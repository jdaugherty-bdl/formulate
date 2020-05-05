namespace formulate.app.Persistence.Internal
{

    // Namespaces.
    using Entities;
    using Helpers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;


    /// <summary>
    /// Helps with JSON persistence.
    /// </summary>
    internal class JsonPersistenceHelper
    {

        #region Properties

        /// <summary>
        /// The base folder path to store files in.
        /// </summary>
        private string BasePath { get; set; }


        /// <summary>
        /// The file extension to store files with.
        /// </summary>
        private string Extension { get; set; }


        /// <summary>
        /// The wildcard pattern used to find entity files.
        /// </summary>
        private string WildcardPattern { get; set; }

        //private static Dictionary<string, Tuple<long, DateTime>> LastJsonFileList { get; set; }
        private static Dictionary<string, Tuple<Type, object, long, DateTime>> CurrentEntityList { get; set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Full constructor.
        /// </summary>
        /// <param name="basePath">The base path to store files in.</param>
        /// <param name="extension">The extension to store files with.</param>
        /// <param name="wildcard">
        /// The wildcard pattern that can be used to find entity files.
        /// </param>
        public JsonPersistenceHelper(string basePath, string extension, string wildcard)
        {
            this.BasePath = basePath;
            this.Extension = extension;
            this.WildcardPattern = wildcard;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Writes the specified file at the specified path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="contents">The contents of the file.</param>
        /// <remarks>
        /// Will ensure the folder exists before attempting to write to the file.
        /// </remarks>
        public void WriteFile(string path, string contents)
        {

            // Ensure folder exists.
            var folderPath = Path.GetDirectoryName(path);
            EnsurePathExists(folderPath);


            // Write file contents.
            File.WriteAllText(path, contents);

        }


        /// <summary>
        /// Gets the contents of the file at the specified path.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>
        /// The file contents, or null.
        /// </returns>
        public string GetFileContents(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// Gets the file path to the entity with the specified ID.
        /// </summary>
        /// <param name="entityId">The entity's ID.</param>
        /// <returns>The file to the entity's file.</returns>
        public string GetEntityPath(Guid entityId)
        {
            var id = GuidHelper.GetString(entityId);
            var path = Path.Combine(BasePath, id + Extension);
            return path;
        }


        /// <summary>
        /// Ensures that the specified path exists.
        /// </summary>
        /// <param name="path">The path.</param>
        public void EnsurePathExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }


        /// <summary>
        /// Persists a entity to the file system.
        /// </summary>
        /// <param name="entityId">The ID of the entity.</param>
        /// <param name="entity">The entity to persist.</param>
        public void Persist(Guid entityId, object entity)
        {
            var path = GetEntityPath(entityId);
            var serialized = JsonHelper.Serialize(entity);
            WriteFile(path, serialized);
        }


        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="entityId">The ID of the entity to delete.</param>
        public void Delete(Guid entityId)
        {
            // if the singleton isn't set up, then create a new one
            if (CurrentEntityList == null)
                CurrentEntityList = new Dictionary<string, Tuple<Type, object, long, DateTime>>();

            var path = GetEntityPath(entityId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (CurrentEntityList.ContainsKey(path))
                CurrentEntityList.Remove(path);
        }


        /// <summary>
        /// Gets the entity with the specified ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity.</param>
        /// <returns>
        /// The entity.
        /// </returns>
        public EntityType Retrieve<EntityType>(Guid entityId)
        {
            var path = GetEntityPath(entityId);

            return ReadFileIfNecessary<EntityType>(path);
        }


        /// <summary>
        /// Gets all the entities that are the children of the folder with the specified ID.
        /// </summary>
        /// <param name="parentId">The parent ID.</param>
        /// <returns>
        /// The entities.
        /// </returns>
        /// <remarks>
        /// You can specify a parent ID of null to get the root entities.
        /// </remarks>
        public IEnumerable<EntityType> RetrieveChildren<EntityType>(Guid? parentId)
            where EntityType: IEntity
        {
            // TODO: Optimize this. I'm reading in all entities just to get a subset of them.
            var entities = RetrieveAll<EntityType>();
            if (parentId.HasValue)
            {
                // Return entities under folder.
                return entities.Where(x => x.Path[x.Path.Length - 2] == parentId.Value);
            }
            else
            {
                // Return root entities.
                return entities.Where(x => x.Path.Length == 2);
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Gets all entities of the specified type.
        /// </summary>
        /// <typeparam name="EntityType">
        /// The type of entity.
        /// </typeparam>
        /// <returns>
        /// The entities.
        /// </returns>
        public IEnumerable<EntityType> RetrieveAll<EntityType>()
        {
            // make sure the root folder exists first
            if (Directory.Exists(BasePath))
            {
                // get all the files in the directory
                /*
                var files = Directory.GetFiles(BasePath, WildcardPattern);
                foreach (var file in files)
                {
                    // read from cache
                    ReadFileIfNecessary<EntityType>(file);
                }
                */

                ReadAllFilesIfNecessary<EntityType>();
            }

            // return all of EntityType entities
            return CurrentEntityList
                .Where(x => x.Value.Item1 == typeof(EntityType))
                .Select(x => (EntityType)x.Value.Item2);
        }

        private IEnumerable<EntityType> ReadAllFilesIfNecessary<EntityType>()
        {
            // if the singleton isn't set up, then create a new one
            if (CurrentEntityList == null)
                CurrentEntityList = new Dictionary<string, Tuple<Type, object, long, DateTime>>();

            var updatedFiles = new List<string>();

            var directoryInfoListing = new DirectoryInfo(BasePath)
                .GetFiles(WildcardPattern, SearchOption.TopDirectoryOnly);

            var fileNamesList = directoryInfoListing
                .Select(x => x.FullName)
                .ToList();

            var existingFiles = fileNamesList
                .Where(CurrentEntityList.ContainsKey)
                .Select(x => directoryInfoListing.Where(y => y.FullName == x).FirstOrDefault())
                .ToDictionary(x => x.FullName, x => new Tuple<long, DateTime>(x.Length, x.LastWriteTime));

            var unreadFiles = fileNamesList
                .Except(existingFiles.Keys);

            foreach (var unreadFile in unreadFiles)
            {
                var fixedFile = Path.Combine(Path.GetDirectoryName(unreadFile), Path.GetFileName(unreadFile));

                var ent = ReadFileContents<EntityType>(fixedFile);

                if (ent != null) //default)
                    updatedFiles.Add(fixedFile);
            }

            foreach (var existingFile in existingFiles)
            {
                var fixedFile = Path.Combine(Path.GetDirectoryName(existingFile.Key), Path.GetFileName(existingFile.Key));

                // force a reload if the file has been updated
                if (!updatedFiles.Contains(fixedFile) && (!CurrentEntityList.ContainsKey(fixedFile) || CurrentEntityList[fixedFile].Item3 != existingFile.Value.Item1 || CurrentEntityList[fixedFile].Item4 != existingFile.Value.Item2))
                {
                    var ent = ReadFileContents<EntityType>(fixedFile);

                    if (ent != null) //default)
                        updatedFiles.Add(fixedFile);
                }
            }

            return CurrentEntityList
                .Where(x => updatedFiles.Contains(x.Key))
                .Select(x => (EntityType)x.Value.Item2);
        }

        /// <summary>
        /// Checks if the file has not already been read to the singleton or if the file contents have changed, and reread file contents if they are.
        /// </summary>
        /// <typeparam name="EntityType">The object type to convert file contents to.</typeparam>
        /// <param name="file">The full path of the file to read.</param>
        /// <returns>An object of type EntityType as read from the JSON file.</returns>
        private EntityType ReadFileIfNecessary<EntityType>(string file)
        {
            if (!File.Exists(file))
                return default;

            var fixedFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileName(file));
            
            // if the singleton isn't set up, then create a new one
            if (CurrentEntityList == null)
                CurrentEntityList = new Dictionary<string, Tuple<Type, object, long, DateTime>>();

            // if the file hasn't been read (or has been cleared from the cache), just reread the file
            if (!CurrentEntityList.ContainsKey(fixedFile)) //CurrentEntityList.ContainsKey(file))
            {
                return ReadFileContents<EntityType>(fixedFile);
            }
            else
            {
                // check file size and date/time - if either has changed, force file reread
                var fileInfo = new FileInfo(fixedFile);

                // force a reload if the file has been updated
                if (CurrentEntityList[fixedFile].Item3 != fileInfo.Length || CurrentEntityList[fixedFile].Item4 != fileInfo.LastWriteTime)
                    return ReadFileContents<EntityType>(fixedFile); 
            }

            // if no reread is necessary, just return the object from cache
            return (EntityType)CurrentEntityList[fixedFile].Item2;
        }

        /// <summary>
        /// Reads the file contents of the specified path and adds it and the file information to a singleton.
        /// </summary>
        /// <typeparam name="EntityType">The object type to convert file contents to.</typeparam>
        /// <param name="file">The full path of the file to read.</param>
        /// <returns>An object of type EntityType as read from the JSON file.</returns>
        private EntityType ReadFileContents<EntityType>(string file)
        {
            if (!File.Exists(file))
                return default;

            var fixedFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileName(file));

            // if the singleton isn't set up, then create a new one
            if (CurrentEntityList == null)
                CurrentEntityList = new Dictionary<string, Tuple<Type, object, long, DateTime>>();

            // if we're here, we're forcing a reread
            if (CurrentEntityList.ContainsKey(fixedFile))
                CurrentEntityList.Remove(fixedFile);

            // read the file and convert
            var contents = GetFileContents(fixedFile);
            var entity = JsonHelper.Deserialize<EntityType>(contents);

            //entities.Add(entity);

            // get the size and date/time of last write
            var fileInfo = new FileInfo(fixedFile);

            // save to singleton
            CurrentEntityList.Add(fixedFile, new Tuple<Type, object, long, DateTime>(typeof(EntityType), entity, fileInfo.Length, fileInfo.LastWriteTime));

            // return this entity for ease of use by caller
            return entity;
        }

        #endregion

    }

}