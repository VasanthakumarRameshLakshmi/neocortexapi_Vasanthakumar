﻿
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AkkaSb.Net
{
    public class BlobStoragePersistenceProvider : IPersistenceProvider
    {
        const string cConnStr = "StorageConnectionString";

        private string actorSystemId;

        private CloudBlobClient blobClient;

        private CloudBlobContainer cloudBlobContainer;

        private ILogger logger;


        public async Task InitializeAsync(string actorSystemId, Dictionary<string, object> settings, bool purgeOnStart = false, ILogger logger = null)
        {
            this.actorSystemId = actorSystemId;

            this.logger = logger;

            if (!settings.ContainsKey(cConnStr))
                throw new ArgumentException($"'{cConnStr}' argument must be contained in settings.");

            CloudStorageAccount storageAccount = CreateFromConnectionString(settings[cConnStr] as string);

            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

            string containerName = $"actsys{actorSystemId}";

            cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);

            if (purgeOnStart == true)
            {
                await Purge();
            }

            if (await cloudBlobContainer.CreateIfNotExistsAsync())
            {
                this.logger?.LogTrace("Created container : {0}", containerName);
            }
            else
            {
                this.logger?.LogTrace("Created container : {0}", containerName);
            }
        }

        public async Task<ActorBase> LoadActor(ActorId actorId)
        {
            this.logger?.LogTrace("Loading actor: {0}", actorId);

            var actorInstance = await GetPersistedActorAsync(actorId);

            if (actorInstance == null)
                this.logger?.LogTrace("Actor: {0} was not found in persistence store.", actorId);
            else
                this.logger?.LogTrace("Actor: {0} loaded from persistence store.", actorId);

            return actorInstance;
        }


        public async Task PersistActor(ActorBase actorInstance)
        {
            this.logger?.LogTrace("Persisting actor: {0}", actorInstance.Id);

            var serializedActor = SerializeActor(actorInstance);

            await SaveActorToBlob(actorInstance.Id, serializedActor);

            this.logger?.LogTrace("Persisting actor: {0}", actorInstance.Id);
        }


        public async Task Purge()
        {
            this.logger?.LogTrace("Purge started");

            await this.cloudBlobContainer.DeleteIfExistsAsync();

            this.logger?.LogTrace("Purge completed");
        }

        #region Private Methods

        private async Task<ActorBase> GetPersistedActorAsync(string actorId)
        {
            string blobName = getBlobNameFromId(actorId);

            if (await this.cloudBlobContainer.GetBlockBlobReference(blobName).ExistsAsync())
            {
                var blob = await this.cloudBlobContainer.GetBlockBlobReference(blobName).DownloadTextAsync();
                return DeserializeActor<ActorBase>(blob);
            }
            else
                return null;              
        }

        private async Task SaveActorToBlob(string actorId,string serializedActor)
        {
            if (serializedActor == null)
            {
                throw new ArgumentNullException("Entity cannot be null!");
            }
            try
            {
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(getBlobNameFromId(actorId));
                await cloudBlockBlob.UploadTextAsync(serializedActor);
            }
            catch (StorageException ex)
            {
                this.logger.LogError(ex.Message, "Failed to create the blob.");
                throw;
            }
        }

        private static string getBlobNameFromId(string actorId)
        {
            return $"{actorId}.txt";
        }

        private static CloudStorageAccount CreateFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }

        internal static string SerializeActor(ActorBase actorInstance)
        {
            JsonSerializerSettings sett = new JsonSerializerSettings();
            sett.TypeNameHandling = TypeNameHandling.All;

            var strObj = JsonConvert.SerializeObject(actorInstance, Formatting.Indented, sett);
            
            return strObj;
        }

        internal static T DeserializeActor<T>(string serializedActor)
        {
            JsonSerializerSettings sett = new JsonSerializerSettings();

            sett.TypeNameHandling = TypeNameHandling.All;

            var strObj = JsonConvert.DeserializeObject<T>(serializedActor, sett);

            return strObj;
        }


        #endregion
    }
}
