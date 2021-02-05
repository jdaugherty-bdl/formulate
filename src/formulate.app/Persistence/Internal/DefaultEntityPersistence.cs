﻿namespace formulate.app.Persistence.Internal
{

    // Namespaces.
    using DataValues;
    using Entities;
    using Folders;
    using Forms;
    using Helpers;
    using Layouts;
    using Resolvers;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using Validations;


    /// <summary>
    /// Handles persistence of entities.
    /// </summary>
    internal class DefaultEntityPersistence : IEntityPersistence
    {

        #region Properties

        /// <summary>
        /// Folder persistence.
        /// </summary>
        private IFolderPersistence Folders
        {
            get
            {
                return FolderPersistence.Current.Manager;
            }
        }


        /// <summary>
        /// Form persistence.
        /// </summary>
        private IFormPersistence Forms
        {
            get
            {
                return FormPersistence.Current.Manager;
            }
        }


        /// <summary>
        /// Configured form persistence.
        /// </summary>
        private IConfiguredFormPersistence ConfiguredForms
        {
            get
            {
                return ConfiguredFormPersistence.Current.Manager;
            }
        }


        /// <summary>
        /// Layout persistence.
        /// </summary>
        private ILayoutPersistence Layouts
        {
            get
            {
                return LayoutPersistence.Current.Manager;
            }
        }


        /// <summary>
        /// Validation persistence.
        /// </summary>
        private IValidationPersistence Validations
        {
            get
            {
                return ValidationPersistence.Current.Manager;
            }
        }


        /// <summary>
        /// Layout persistence.
        /// </summary>
        private IDataValuePersistence DataValues
        {
            get
            {
                return DataValuePersistence.Current.Manager;
            }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DefaultEntityPersistence()
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the entity with the specified ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity.</param>
        /// <returns>
        /// The entity.
        /// </returns>
        public IEntity Retrieve(Guid entityId)
        {

            // Root-level node?
            if (EntityHelper.IsRoot(entityId))
            {
                return new EntityRoot()
                {
                    Id = entityId,
                    Path = new[] { entityId },
                    Name = EntityHelper.GetNameForRoot(entityId),
                    Icon = EntityHelper.GetIconForRoot(entityId)
                };
            }
            else
            {

                // Specific entities (e.g., forms or layouts).
                return Folders.Retrieve(entityId) as IEntity
                    ?? Forms.Retrieve(entityId) as IEntity
                    ?? ConfiguredForms.Retrieve(entityId) as IEntity
                    ?? Layouts.Retrieve(entityId) as IEntity
                    ?? Validations.Retrieve(entityId) as IEntity
                    ?? DataValues.Retrieve(entityId) as IEntity;

            }

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
        public IEnumerable<IEntity> RetrieveChildren(Guid? parentId)
        {
            var children = new List<IEntity>();
            children.AddRange(Folders.RetrieveChildren(parentId));
            children.AddRange(Forms.RetrieveChildren(parentId));
            if (parentId.HasValue)
            {
                children.AddRange(ConfiguredForms.RetrieveChildren(parentId.Value));
            }
            children.AddRange(Layouts.RetrieveChildren(parentId));
            children.AddRange(Validations.RetrieveChildren(parentId));

            //2021-02-05 [JD] - commenting this out because we have 4,000 data values and is making the back end time out when there are a lot of children and iterations
            if (bool.Parse(ConfigurationManager.AppSettings["ReadFormulateDataValues"]))
            {
                // TODO: figure out how to get data values without it taking a long time
                children.AddRange(DataValues.RetrieveChildren(parentId));
            }

            return children;
        }


        /// <summary>
        /// Gets all the entities that are the descendants of the folder with the specified ID.
        /// </summary>
        /// <param name="parentId">The parent ID.</param>
        /// <returns>
        /// The entities.
        /// </returns>
        public IEnumerable<IEntity> RetrieveDescendants(Guid parentId)
        {
            var descendants = new List<IEntity>();
            var folders = Folders.RetrieveChildren(parentId);
            var folderDescendants = folders.SelectMany(x => RetrieveDescendants(x.Id));
            descendants.AddRange(folders);
            descendants.AddRange(folderDescendants);
            descendants.AddRange(Forms.RetrieveChildren(parentId));
            descendants.AddRange(ConfiguredForms.RetrieveChildren(parentId));
            descendants.AddRange(Layouts.RetrieveChildren(parentId));
            descendants.AddRange(Validations.RetrieveChildren(parentId));
            descendants.AddRange(DataValues.RetrieveChildren(parentId));
            return descendants;
        }


        /// <summary>
        /// Moves the specified entity under the parent at the specified path.
        /// </summary>
        /// <param name="entity">
        /// The entity to move.
        /// </param>
        /// <param name="parentPath">
        /// The path to the new parent.
        /// </param>
        /// <returns>
        /// The new path.
        /// </returns>
        public Guid[] MoveEntity(IEntity entity, Guid[] parentPath)
        {

            // Update path.
            var path = parentPath.Concat(new[] { entity.Id }).ToArray();
            entity.Path = path;


            // Persist entity based on the entity type.
            if (entity is Form)
            {
                Forms.Persist(entity as Form);
            }
            else if (entity is Layout)
            {
                Layouts.Persist(entity as Layout);
            }
            else if (entity is Validation)
            {
                Validations.Persist(entity as Validation);
            }
            else if (entity is DataValue)
            {
                DataValues.Persist(entity as DataValue);
            }
            else if (entity is Folder)
            {
                Folders.Persist(entity as Folder);
            }
            else if (entity is ConfiguredForm)
            {
                ConfiguredForms.Persist(entity as ConfiguredForm);
            }


            // Return new path.
            return path;

        }


        /// <summary>
        /// Deletes the specified entity.
        /// </summary>
        /// <param name="entity">
        /// The entity to delete.
        /// </param>
        public void DeleteEntity(IEntity entity)
        {

            // Delete entity based on the entity type.
            if (entity is Form)
            {
                Forms.Delete(entity.Id);
            }
            else if (entity is Layout)
            {
                Layouts.Delete(entity.Id);
            }
            else if (entity is Validation)
            {
                Validations.Delete(entity.Id);
            }
            else if (entity is DataValue)
            {
                DataValues.Delete(entity.Id);
            }
            else if (entity is Folder)
            {
                Folders.Delete(entity.Id);
            }
            else if (entity is ConfiguredForm)
            {
                ConfiguredForms.Delete(entity.Id);
            }

        }

        #endregion

    }

}