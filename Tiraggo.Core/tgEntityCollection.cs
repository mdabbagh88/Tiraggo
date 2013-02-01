﻿/*  New BSD License
-------------------------------------------------------------------------------
Copyright (c) 2006-2012, EntitySpaces, LLC
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the EntitySpaces, LLC nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL EntitySpaces, LLC BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
-------------------------------------------------------------------------------
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Runtime.Serialization;

using Tiraggo.Interfaces;
using Tiraggo.DynamicQuery;
using System.Linq;

namespace Tiraggo.Core
{
    /// <summary>
    /// The tgEntityCollection is a collection of <see cref="tgEntity"/> objects. The tgEntityCollection
    /// represents 1 or more records from your database, each record is associated with an esEntity object 
    /// within the collection.
    /// </summary>
    /// <remarks>
    /// The sample below demonstrates how to load all of the Employee's in the database
    /// and order them in decending order by salary.
    /// <code>
    /// EmployeesCollection coll = new EmployeesCollection();
    /// coll.Query.OrderBy(coll.Query.Salary.Descending);
    /// if(coll.Query.Load())
    /// {
    ///		foreach(Employees emp in coll)
    ///		{
    ///			// do something
    ///		}
    ///  
    /// 	// Let's add a record and save it
    /// 	Employees newEmp = coll.AddNew();
    /// 	newEmp.FirstName = "James";
    /// 	newEmp.LastName  = "Murphy";
    /// 	
    /// 	// Save it, you call save on the collection
    /// 	coll.Save();
    /// }
    /// </code>
    /// </remarks>
    [DefaultProperty("Indexer")]
    [CollectionDataContract]
    [Serializable]
    public abstract partial class tgEntityCollection<T> : tgEntityCollectionBase, ICommittable
        where T : tgEntity, new()
    {
        public tgEntityCollection()
        {
            entities.RaiseListChangedEvents = false;
            this.entities.ListChanged += new ListChangedEventHandler(this.OnListChanged);
        }

        /// <summary>
        /// Called internally when debugging, allows you to easily view the collection while debugging
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden | DebuggerBrowsableState.Never)]
        [Browsable(false)]
        virtual protected BindingList<T> Debug
        {
            get
            {
                return this.entities;
             }
        }

        #region Overridden Methods

        /// <summary>
        /// Called by the collection when it needs to create an entity
        /// </summary>
        /// <returns></returns>
        protected override tgEntity CreateEntity()
        {
            return new T();
        }

        internal override IList GetList()
        {
            return entities as IList;
        }

        internal override IEnumerable GetDeletedEntities()
        {
            return this.deletedEntities as IEnumerable;
        }

        internal override IEnumerable GetEntities()
        {
            return this.entities;
        }

        internal override void AddEntityToDeletedList(tgEntity entity)
        {
            if (entity.rowState == tgDataRowState.Modified || entity.rowState == tgDataRowState.Unchanged)
            {
                if (deletedEntities == null)
                {
                    deletedEntities = new List<T>();
                }

                T obj = (T)entity;
                deletedEntities.Add(obj);
            }

            if (entitiesFilterBackup != null && entitiesFilterBackup.Contains((T)entity))
            {
                entitiesFilterBackup.Remove((T)entity);
                OnUpdateViewNotification(this, ListChangedType.ItemDeleted, entity);
            }
        }

        public override Type GetEntityType()
        {
            return typeof(T);
        }

        #endregion


        /// <summary>
        /// Call this method to add another entity to your collection
        /// </summary>
        /// <returns>The newly created entity</returns>
        virtual public T AddNew()
        {
            IBindingList list = this as IBindingList;

            T entity = (T)list.AddNew();

            if (fks != null)
            {
                foreach (string column in fks.Keys)
                {
                    entity.SetColumn(column, fks[column]);
                }
            }

            return entity;
        }

        /// <summary>
        /// Creates a shallow copy of the collection
        /// </summary>
        /// <typeparam name="C"></typeparam>
        /// <returns>The newly created collection</returns>
        virtual public C Clone<C>() where C : tgEntityCollection<T>, new()
        {
            C collection = new C();

            foreach (T entity in entities)
            {
                T newEntity = entity.Clone<T>();
                collection.Add(newEntity);
            }

            return collection;
        }

        public override void CombineDeletedEntities()
        {
            if (this.deletedEntities != null && this.deletedEntities.Count > 0)
            {
                foreach (T entity in deletedEntities)
                {
                    entities.Add(entity);
                }
            }
        }

        public override void SeparateDeletedEntities()
        {
            foreach (T entity in this.entities)
            {
                if (entity.rowState == tgDataRowState.Deleted)
                {
                    if (deletedEntities == null)
                    {
                        deletedEntities = new BindingList<T>();
                    }

                    deletedEntities.Add(entity);
                }
            }

            if (this.deletedEntities != null && this.deletedEntities.Count > 0)
            {
                foreach (T entity in deletedEntities)
                {
                    entities.Remove(entity);
                }
            }
        }

        /// <summary>
        /// Combines two collections into a single collection.
        /// </summary>
        /// <remarks>
        /// After this method executes the collection passed in is no
        /// longer valid and should no longer be used or reloaded.
        /// </remarks>
        /// <example>
        /// This code combines the first three and last three rows from a
        /// table into a single collection.
        /// <code>
        /// EmployeesCollection dest = new EmployeesCollection();
        /// dest.Query.es.Top = 3;
        /// dest.Query.OrderBy(dest.Query.EmployeeID, tgOrderByDirection.Ascending);
        /// dest.Query.Load();
        /// 
        /// EmployeesCollection src = new EmployeesCollection();
        /// src.Query.es.Top = 3;
        /// src.Query.OrderBy(src.Query.EmployeeID, tgOrderByDirection.Descending);
        /// src.Query.Load();
        ///
        /// dest.Combine(src);
        /// </code>
        /// </example>
        /// <param name="collectionToAdd">The collection to add, collectionToAdd is empty after this call</param>
        virtual public void Combine(tgEntityCollection<T> collectionToAdd)
        {
            RaiseListChangeEvents_Disable();

            foreach (tgEntity entity in collectionToAdd.entities)
            {
                entity.Collection = this;

                if (this.fks != null)
                {
                    foreach (string col in this.fks.Keys)
                    {
                        entity.currentValues[col] = this.fks[col];
                    }
                }

                // This entity now takes on the Collection's Connection info
                entity.tg.Connection.Name = this.tg.Connection.Name;

                if (entitiesFilterBackup != null)
                {
                    entitiesFilterBackup.Add((T)entity);
                }
                else
                {
                    entities.Add((T)entity);
                }
            }

            if (currentFilter != null)
            {
                this.Filter = currentFilter;
            }

            collectionToAdd.entities.Clear();
            collectionToAdd.tg.Connection = null;

            RaiseListChangeEvents_Restore();

            OnUpdateViewNotification(this, ListChangedType.Reset, null);

            if (entities.RaiseListChangedEvents)
            {
                this.OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
        }

        public override void MarkAllAsDeleted()
        {
            RaiseListChangeEvents_Disable();

            if (deletedEntities == null)
            {
                deletedEntities = new List<T>();
            }

            foreach (T e in entities)
            {
                if (e.RowState != tgDataRowState.Added)
                {
                    deletedEntities.Add(e);
                    e.rowState = tgDataRowState.Deleted;
                }

                if (entitiesFilterBackup != null)
                {
                    entitiesFilterBackup.Remove(e);
                }
            }

            this.entities.Clear();

            RaiseListChangeEvents_Restore();

            OnUpdateViewNotification(this, ListChangedType.Reset, null);

            if (entities.RaiseListChangedEvents)
            {
                this.OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
        }

        /// <summary>
        /// Called when the Query for this collection has finished loading
        /// </summary>
        /// <param name="query">The Query that was just loaded</param>
        /// <param name="table">The DataTable returned from the EntitySpaces data provider</param>
        /// <returns>True if at least one record was returned</returns>
        protected bool OnQueryLoaded(tgDynamicQuery query, DataTable table)
        {
            this.PopulateCollection(table);

            // Add ourself into the list with a map name of string.Empty
            Dictionary<string, Dictionary<object, tgEntityCollectionBase>> collections = new Dictionary<string, Dictionary<object, tgEntityCollectionBase>>();
            Dictionary<object, tgEntityCollectionBase> me = new Dictionary<object, tgEntityCollectionBase>();
            me[string.Empty] = this;
            collections[string.Empty] = me;

            if (query.tg2.PrefetchMaps != null)
            {
                foreach (tgPrefetchMap map in query.tg2.PrefetchMaps)
                {
                    DataTable preFetchedTable = map.Table;

                    Dictionary<object, tgEntityCollectionBase> loadedCollections = null;

                    if (map.Path == string.Empty)
                    {
                        loadedCollections = collections[string.Empty];
                    }
                    else
                    {
                        loadedCollections = collections[map.Path];
                    }

                    Dictionary<object, tgEntityCollectionBase> newCollection = new Dictionary<object, tgEntityCollectionBase>();
                    foreach (tgEntityCollectionBase collection in loadedCollections.Values)
                    {
                        foreach (tgEntity obj in collection)
                        {
                            obj.tg.IsLazyLoadDisabled = true;

                            object key = null;

                            // Avoid doing the Split() if we can
                            if (map.IsMultiPartKey)
                            {
                                key = string.Empty;

                                string[] columns = map.ParentColumnName.Split(',');
                                foreach (string col in columns)
                                {
                                    key += Convert.ToString(obj.GetColumn(col));
                                }
                            }
                            else
                            {
                                key = obj.GetColumn(map.ParentColumnName);
                            }

                            IEntity iEntity = obj as IEntity;
                            newCollection[key] = iEntity.CreateCollection(map.PropertyName);
                        }
                    }

                    Dictionary<string, int> ordinals = null;
                    DataRowCollection rows = preFetchedTable.Rows;
                    int count = rows.Count;
                    for (int i = 0; i < count; i++)
                    {
                        DataRow row = rows[i];

                        object key = null;

                        // Avoid doing the Split() if we can
                        if (map.IsMultiPartKey)
                        {
                            key = string.Empty;

                            string[] columns = map.MyColumnName.Split(',');
                            foreach (string col in columns)
                            {
                                key += Convert.ToString(row[col]);
                            }
                        }
                        else
                        {
                            key = row[map.MyColumnName];
                        }

                        tgEntityCollectionBase c = newCollection[key];

                        IEntityCollection iColl = c as IEntityCollection;
                        ordinals = iColl.PopulateCollection(row, ordinals);
                    }

                    collections[map.PropertyName] = newCollection;
                }
            }

            return (this.Count > 0) ? true : false;
        }

        #region Filter

        /// <summary>
        /// Use LINQ expressions to filter your collection
        /// </summary>
        /// <remarks>
        /// The examples below demonstrates how to Filter and Sort, seperately and together. To clear the filter
        /// merely set this property to 'null' or 'Nothing'
        /// <code>
        /// // Sorting by the Employee’s first name
        /// coll.Filter = coll.AsQueryable().OrderByDescending(d => d.FirstName);
        /// 
        /// // Filtering for all Empoyees missing their FirstName
        /// coll.Filter = coll.AsQueryable().Where(d => d.FirstName == null);
        /// 
        /// // Both Filtering and Sorting
        /// coll.Filter = coll.AsQueryable().Where(d => d.FirstName == null).OrderByDescending(d => d.LastName);
        /// </code>
        /// </remarks>    
  
        [Browsable(false)]
        public IQueryable<T> Filter
        {
            get
            {
                return currentFilter;
            }

            set
            {
                RemoveFilter();

                if (value != null)
                {
                    ApplyFilter(value);
                }
                currentFilter = value;

                if (entities.RaiseListChangedEvents)
                {
                    OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                }
            }
        }

        /// <summary>
        /// Returns true if the collection is currently filtered via the Filter property
        /// </summary>
        [Browsable(false)]
        public bool HasFilter
        {
            get
            {
                return currentFilter != null ? true : false;
            }
        }

        private void RemoveFilter()
        {
            currentFilter = null;

            if (entitiesFilterBackup == null) return;

            RaiseListChangeEvents_Disable();
            entities.Clear();

            foreach (T obj in entitiesFilterBackup)
            {
                entities.Add(obj);
            }
            entitiesFilterBackup = null;

            RaiseListChangeEvents_Restore();
        }

        private void ApplyFilter(IQueryable<T> filter)
        {
            BindingList<T> temp = new BindingList<T>();

            foreach (T obj in entities)
            {
                temp.Add(obj);
            }

            RaiseListChangeEvents_Disable();

            List<T> list = filter.ToList<T>();
            entities.Clear();
            foreach (T obj in list)
            {
                entities.Add(obj);
            }

            entitiesFilterBackup = temp;

            RaiseListChangeEvents_Restore();
        }

        #endregion

        /// <summary>
        /// The number of entities in the collection
        /// </summary>
        [Browsable(false)]
        public override int Count
        {
            get
            {
                return entities.Count;
            }
        }

        /// <summary>
        /// This method returns true if this Collection has been loaded with data 
        /// and has at least one row of data that isn't marked as deleted.
        /// </summary>
        virtual public bool HasData
        {
            get
            {
                return entities.Count > 0 ? true : false;
            }
        }

        /// <summary>
        /// This is set to true if AddNew(), MarkAsDeleted(), or if any of the contained esEntities table based properties 
        /// have been modified. After a successful call to <see cref="Save"/> IsDirty will report false.
        /// </summary>
        public override bool IsDirty
        {
            get
            {
                if (entities.Count > 0)
                {
                    foreach(tgEntity entity in entities)
                    {
                        switch (entity.RowState)
                        {
                            case tgDataRowState.Added:
                                return true;
                            case tgDataRowState.Modified:
                                return true;
                            case tgDataRowState.Deleted:
                                return true;
                        }
                    }
                }

                if (deletedEntities != null && deletedEntities.Count > 0)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// The indexer for the collection. Using the foreach() syntax is preferred and faster.
        /// </summary>
        /// <param name="index">The zero based index of the desired entity</param>
        /// <returns>The entity</returns>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public T this[int index]
        {
            get
            {
                return entities[index];
            }
            set
            {
                entities[index] = value;
            }
        }

        #region RaiseListChangedEvents Processing

        [NonSerialized]
        private bool raiseListChangedEvents = false;

        public void RaiseListChangeEvents_Disable()
        {
            raiseListChangedEvents = entities.RaiseListChangedEvents;
            entities.RaiseListChangedEvents = false;
        }

        public void RaiseListChangeEvents_Restore()
        {
            entities.RaiseListChangedEvents = raiseListChangedEvents;
        }

        public bool RaiseListChangeEventsEnabled
        {
            get
            {
                return entities.RaiseListChangedEvents;
            }
        }

        #endregion

        /// <summary>
        /// This method is always called when a collection is loaded and is responsible for turning the
        /// ADO.NET DataTable into an EntitySpaces collection
        /// </summary>
        /// <param name="table">The DataTable containing the data to be loaded</param>
        protected void PopulateCollection(DataTable table)
        {
            RaiseListChangeEvents_Disable();

            try
            {
                this.entities.Clear();

                // Now let's actually create an esEntity for each DataRow and thereby populate
                // the collection
                string columnName;
                tgColumnMetadataCollection esCols = Meta.Columns;
                tgColumnMetadata esCol;
                DataRow row;
                DataColumnCollection cols = table.Columns;
                DataRowCollection rows = table.Rows;
                int rowCount = rows.Count;
                int colCount = cols.Count;

                Dictionary<string, int> ordinals = new Dictionary<string, int>(colCount);

                for(int c = 0; c < colCount; c++)
                {
                    DataColumn col = cols[c];

                    // Let's make sure we use the Case in the Metadata Class, if they return "employeeid" in a proc
                    // this little trick will make sure we use "EmployeeId" for our property accessors
                    esCol = esCols.FindByColumnName(col.ColumnName);
                    columnName = esCol != null ? esCol.Name : col.ColumnName;

                    ordinals[columnName] = col.Ordinal;

                    if (selectedColumns == null)
                    {
                        selectedColumns = new Dictionary<string, int>(cols.Count);
                    }

                    if (esCol != null)
                    {
                        selectedColumns[columnName] = c;
                    }

                    if (esCol == null)
                    {
                        if (extraColumnMetadata == null)
                        {
                            extraColumnMetadata = new Dictionary<string, tgColumnMetadata>(cols.Count);
                        }

                        extraColumnMetadata[columnName] = new tgColumnMetadata(columnName, c, col.DataType);
                    }
                }

                bool isLazyLoadDisabled = this.tg.IsLazyLoadDisabled;
                for (int i = 0; i < rowCount; i++)
                {
                    T obj = AddNew();
                    obj.tg.IsLazyLoadDisabled = isLazyLoadDisabled;

                    row = rows[i];

                    object[] values = row.ItemArray;

                    obj.currentValues = new tgSmartDictionary(ordinals, values);
                    obj.originalValues = new tgSmartDictionary(ordinals, values, true);
                    if (obj.m_modifiedColumns != null) obj.m_modifiedColumns = null;
                    obj.rowState = tgDataRowState.Unchanged;
                }
            }
            finally
            {
                RaiseListChangeEvents_Restore();
                OnUpdateViewNotification(this, ListChangedType.Reset, null);

                if (entities.RaiseListChangedEvents)
                {
                    OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                }
            }
        }

        protected override Dictionary<string, int> PopulateCollectionPrefetch(DataRow row, Dictionary<string, int> ordinals)
        {
            #region Column Metadata
            if (this.selectedColumns == null)
            {
                // Now let's actually create an esEntity for each DataRow and thereby populate
                // the collection
                string columnName;
                tgColumnMetadataCollection esCols = Meta.Columns;
                tgColumnMetadata esCol;
                DataColumnCollection cols = row.Table.Columns;
                DataRowCollection rows = row.Table.Rows;
                int rowCount = rows.Count;
                int colCount = cols.Count;

                ordinals = new Dictionary<string, int>(colCount);

                for (int c = 0; c < colCount; c++)
                {
                    DataColumn col = cols[c];

                    // Let's make sure we use the Case in the Metadata Class, if they return "employeeid" in a proc
                    // this little trick will make sure we use "EmployeeId" for our property accessors
                    esCol = esCols.FindByColumnName(col.ColumnName);
                    columnName = esCol != null ? esCol.Name : col.ColumnName;

                    ordinals[columnName] = col.Ordinal;

                    if (selectedColumns == null)
                    {
                        selectedColumns = new Dictionary<string, int>(cols.Count);
                    }

                    if (esCol != null)
                    {
                        selectedColumns[columnName] = c;
                    }

                    if (esCol == null)
                    {
                        if (extraColumnMetadata == null)
                        {
                            extraColumnMetadata = new Dictionary<string, tgColumnMetadata>(cols.Count);
                        }

                        extraColumnMetadata[columnName] = new tgColumnMetadata(columnName, c, col.DataType);
                    }
                }
            }
            #endregion

            T obj = AddNew();

            object[] values = row.ItemArray;

            obj.currentValues = new tgSmartDictionary(ordinals, values);
            obj.originalValues = new tgSmartDictionary(ordinals, values, true);
            if (obj.m_modifiedColumns != null) obj.m_modifiedColumns = null;
            obj.rowState = tgDataRowState.Unchanged;

            IEntity iEntity = obj as IEntity;
            obj.tg.IsLazyLoadDisabled = true;

            return ordinals;
        }

        /// <summary>
        /// Called internally anytime the tgEntityCollection class is about to make a call to
        /// one of the EntitySpaces' DataPoviders. This method wraps up most of the common logic
        /// required to make the actual call. 
        /// </summary>
        /// <returns>tgDataRequest</returns>
        /// <seealso cref="tgDataRequest"/><seealso cref="tgDataProvider"/><seealso cref="IDataProvider"/>
        protected tgDataRequest CreateRequest()
        {
            tgDataRequest request = new tgDataRequest();
            request.Caller = this;

            tgConnection conn = this.tg.Connection;
            tgProviderSpecificMetadata providerMetadata = this.Meta.GetProviderMetadata(conn.ProviderMetadataKey);

            request.ConnectionString = conn.ConnectionString;
            request.CommandTimeout = conn.CommandTimeout;
            request.DataID = this.Meta.DataID;

            request.Catalog = conn.Catalog;
            request.Schema = conn.Schema;

            request.ProviderMetadata = this.Meta.GetProviderMetadata(conn.ProviderMetadataKey);
            request.SelectedColumns = selectedColumns;

            return request;
        }

        /// <summary>
        /// If an error occurs during Save you can iterate through the errors. You use foreach
        /// to iterator over the esEntity's who had an error during save checking their RowError
        /// property
        /// </summary>
        /// <example>
        /// <code>
        /// EmployeesCollection coll = new EmployeesCollection();
        /// // change some data ...
        /// try
        /// {
        ///    coll.Save();
        /// }
        /// catch
        /// {
        ///    foreach (Employees emp in coll1.Errors)
        ///    { 
        ///        Console.WriteLine(emp.RowError);
        ///    }
        /// }
        /// </code>
        /// </example>
        public IEnumerable<T> Errors
        {
            get
            {
                if (deletedEntities == null)
                {
                    return from e in entities where e.rowError != null select e;
                }
                else
                {
                    return (from e in entities where e.rowError != null select e).Union
                           (from d in deletedEntities where d.rowError != null select d);
                }
            }
        }

        /// <summary>
        /// Attaches the entity to this collection.
        /// </summary>
        /// <param name="entity">The entity you wish to attach to the collection.</param>
        /// <returns>The entity just added</returns>
        virtual public T AttachEntity(T entity)
        {
            entity.Collection = this;

            if (entity.tg.IsDeleted)
            {
                if (deletedEntities == null)
                {
                    deletedEntities = new List<T>();
                }

                deletedEntities.Add(entity);
                return entity;
            }

            // This entity now takes on the Collection's Connection info
            entity.tg.Connection.Name = this.tg.Connection.Name;

            entities.Add(entity);

            if (HasFilter && entitiesFilterBackup != null)
            {
                entitiesFilterBackup.Add(entity);
            }

            OnUpdateViewNotification(this, ListChangedType.ItemAdded, entity);

            return entity;
        }

        /// <summary>
        /// Detaches the entity.
        /// </summary>
        /// <param name="entity">The entity you wish to detach from the collection.</param>
        /// <returns>The entity just detached</returns>
        virtual public T DetachEntity(T entity)
        {
            entity.Collection = null;

            entities.Remove(entity);

            if (HasFilter && entitiesFilterBackup != null)
            {
                entitiesFilterBackup.Remove(entity);
            }

            //// This entity now takes on the Collection's Connection info
            //entity.es.Connection.Name = this.es.Connection.Name;

            OnUpdateViewNotification(this, ListChangedType.ItemDeleted, entity);

            return entity;
        }

        internal override void RemoveEntity(tgEntity entity)
        {
            DetachEntity((T)entity);
        }

        internal override void ClearDeletedEntries()
        {
            if (deletedEntities != null)
            {
                deletedEntities.Clear();
                deletedEntities = null;
            }
        }

        /// <summary>
        /// Makes all of the proposed values be the current values. You should not call this before Save(). In fact,
        /// in most case you will never call this method.
        /// </summary>
        public override void AcceptChanges()
        {
            foreach (tgEntity entity in entities)
            {
                // Leave errors with rows in their current state
                if (entity.rowError == null)
                {
                    entity.AcceptChanges();
                }
            }

            if (deletedEntities != null)
            {
                for (int i = deletedEntities.Count - 1; i >= 0; i--)
                {
                    tgEntity entity = deletedEntities[i];
                    if (entity.rowError == null)
                    {
                        deletedEntities.Remove((T)entity);
                    }
                }

                if (deletedEntities.Count == 0)
                {
                    deletedEntities = null;
                }
            }
        }

        /// <summary>
        /// Rolls back all changes that have been made to the table since it was loaded, or the last time AcceptChanges was called.
        /// </summary>
        public override void RejectChanges()
        {
            RaiseListChangeEvents_Disable();

            // Move the deleted rows back into the main entity list
            if (deletedEntities != null)
            {
                foreach (T obj in deletedEntities)
                {
                    entities.Add(obj);

                    // Add it to the filtered list too if need be
                    if (entitiesFilterBackup != null)
                    {
                        entitiesFilterBackup.Add(obj);
                    }
                }
            }
            deletedEntities = null;
           
            List<tgEntity> addedRows = new List<tgEntity>();
            foreach (T entity in entities)
            {
                if(entity.rowState == tgDataRowState.Added)
                {
                    // We're going to remove this in a moment
                    addedRows.Add(entity);
                }
                else
                {
                    if (entity.rowState == tgDataRowState.Modified || entity.rowState == tgDataRowState.Deleted)
                    {
                        entity.RejectChanges();
                    }
                }
            }

            foreach (T entity in addedRows)
            {
                entities.Remove(entity);
            }

            RaiseListChangeEvents_Restore();

            if (entities.RaiseListChangedEvents)
            {
                OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
            }
        }

        /// <summary>
        /// Currently only supported for Microsoft SQL Server. Can only be used for bulk inserts.
        /// This method does not fill in auto-identity columns or default columns values.
        /// </summary>
        virtual public void BulkInsert()
        {
            BulkInsert(null);           
        }

        /// <summary>
        /// Currently only supported for Microsoft SQL Server. Can only be used for bulk inserts.
        /// This method does not fill in auto-identity columns or default columns values.
        /// </summary>
        /// <param name="bulkInsertOptions">An example would be BulkInsert("KeepNulls", "FireTriggers") see the BulkCopyOptions for the ADO.NET provider in question</param>
        virtual public void BulkInsert(params string[] bulkInsertOptions)
        {
            if (entities.Count == 0) return;

            bool needToSendEvent = false;

            try
            {
                using (tgTransactionScope scope = new tgTransactionScope())
                {
                    tgDataRequest request;
                    tgDataProvider provider;
                    tgDataResponse response;

                    this.saveNestingCount++;

                    //=============================================================================
                    // SAVE INSERTS AND UPDATES
                    //=============================================================================
                    if (entities.Count > 0)
                    {
                        // Save added and modified rows. At this point we have removed the deleted rows
                        // and this.Table contains only, inserted, modified, and unchanged rows.
                        request = this.CreateRequest();
                        request.CollectionSavePacket = new List<tgEntitySavePacket>();

                        request.OnError += new tgDataRequest.OnErrorHandler(request_OnError);
                        request.ContinueUpdateOnError = true; // continueUpdateOnError;

                        // Execute PreSave logic
                        foreach (tgEntity entity in this.entities)
                        {
                            entity.rowError = null;

                            entity.PrepareSpecialFields();

                            if (entity.tg.RowState == tgDataRowState.Added)
                            {
                                tgEntitySavePacket packet;
                                packet.OriginalValues = entity.originalValues;
                                packet.CurrentValues = entity.currentValues;
                                packet.RowState = entity.rowState;
                                packet.ModifiedColumns = entity.tg.ModifiedColumns;
                                packet.Entity = entity;

                                request.CollectionSavePacket.Add(packet);
                            }
                        }

                        request.Columns = this.Meta.Columns;
                        request.BulkSave = true;
                        request.BulkSaveOptions = bulkInsertOptions;

                        if (request.CollectionSavePacket.Count > 0)
                        {
                            // Make the call to the DataProvider to physically commit the changes
                            provider = new tgDataProvider();
                            response = provider.esSaveDataTable(request, this.tg.Connection.ProviderSignature);
                            needToSendEvent = true;

                            if (response.IsException)
                            {
                                throw response.Exception;
                            }
                        }
                    }

                    this.AcceptChanges();

                    scope.Complete();
                }
            }
            finally
            {
                this.saveNestingCount--;

                if (this.saveNestingCount == 0 && needToSendEvent == true)
                {
                    OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
                }
            }  
        }

        #region Save

        /// <summary>
        /// Called to save all of the modified entities in the collection. 
        /// </summary>
        /// <remarks>
        /// The tgSqlAccessType is determined by the current connections 'sqlAccessType'
        /// as stored in the app/web config file.  The value is either DynamicSQL or StoredProcedure.
        /// </remarks>
        override public void Save()
        {
            IEntityCollection coll = this as IEntityCollection;
            this.Save(coll.Connection.SqlAccessType, false);
        }

        /// <summary>
        /// Called to save all of the modified entities in the collection. 
        /// </summary>
        /// <param name="continueUpdateOnError">If true, continues updating when other records fail to update</param>
        override public void Save(bool continueUpdateOnError)
        {
            IEntityCollection coll = this as IEntityCollection;
            this.Save(coll.Connection.SqlAccessType, continueUpdateOnError);
        }

        /// <summary>
        /// Called to save all of the modified entities in the collection.
        /// </summary>
        /// <remarks>
        /// The tgSqlAccessType is determined by the current connections 'sqlAccessType'
        /// as stored in the app/web config file.  The value is either DynamicSQL or StoredProcedure.
        /// </remarks>
        /// <param name="sqlAccessType">DynamicSQL or StoredProcedure</param>
        virtual public void Save(tgSqlAccessType sqlAccessType)
        {
            Save(sqlAccessType, false);
        }


        /// <summary>
        /// Called to save all of the modified entities in the collection.
        /// </summary>
        /// <remarks>
        /// The tgSqlAccessType is determined by the current connections 'sqlAccessType'
        /// as stored in the app/web config file.  The value is either DynamicSQL or StoredProcedure.
        /// </remarks>
        /// <param name="sqlAccessType">DynamicSQL or StoredProcedure</param>
        /// <param name="continueUpdateOnError">If true, continues updating when other records fail to update</param>
        virtual public void Save(tgSqlAccessType sqlAccessType, bool continueUpdateOnError)
        {
            if (entities.Count == 0 && deletedEntities == null) return;

            bool needToSendEvent = false;

            try
            {
                using (tgTransactionScope scope = new tgTransactionScope())
                {
                    tgDataRequest request;
                    tgDataProvider provider;
                    tgDataResponse response;

                    this.saveNestingCount++;

                    //=============================================================================
                    // SAVE INSERTS AND UPDATES
                    //=============================================================================
                    if (entities.Count > 0)
                    {
                        // Save added and modified rows. At this point we have removed the deleted rows
                        // and this.Table contains only, inserted, modified, and unchanged rows.
                        request = this.CreateRequest();
                        request.CollectionSavePacket = new List<tgEntitySavePacket>();

                        request.OnError += new tgDataRequest.OnErrorHandler(request_OnError);
                        request.ContinueUpdateOnError = continueUpdateOnError;

                        // Execute PreSave logic
                        foreach (tgEntity entity in this.entities)
                        {
                            entity.rowError = null;

                            entity.PrepareSpecialFields();

                            entity.CommitPreSaves();
                            entity.ApplyPreSaveKeys();

                            if (entity.tg.RowState == tgDataRowState.Added || entity.tg.RowState == tgDataRowState.Modified)
                            {
                                tgEntitySavePacket packet;
                                packet.OriginalValues = entity.originalValues;
                                packet.CurrentValues = entity.currentValues;
                                packet.RowState = entity.rowState;
                                packet.ModifiedColumns = entity.tg.ModifiedColumns;
                                packet.Entity = entity;

                                request.CollectionSavePacket.Add(packet);
                            }
                        }

                        request.Columns = this.Meta.Columns;
                        request.SqlAccessType = sqlAccessType;


                        if (request.CollectionSavePacket.Count > 0)
                        {
                            // Make the call to the DataProvider to physically commit the changes
                            provider = new tgDataProvider();
                            response = provider.esSaveDataTable(request, this.tg.Connection.ProviderSignature);
                            needToSendEvent = true;

                            if (!continueUpdateOnError && response.IsException)
                            {
                                throw response.Exception;
                            }
                        }

                        // PostSave action
                        foreach (tgEntity entity in this.entities)
                        {
                            entity.ApplyPostSaveKeys();
                            entity.ApplyPostOneSaveKeys();
                            entity.CommitPostSaves();
                            entity.CommitPostOneSaves();
                        }
                    }

                    //=============================================================================
                    // SAVE DELETES
                    //=============================================================================
                    if (deletedEntities != null && deletedEntities.Count > 0)
                    {
                        request = this.CreateRequest();
                        request.CollectionSavePacket = new List<tgEntitySavePacket>();

                        request.OnError += new tgDataRequest.OnErrorHandler(request_OnError);
                        request.ContinueUpdateOnError = continueUpdateOnError;

                        foreach (tgEntity entity in deletedEntities)
                        {
                            entity.rowError = null;
                            entity.ApplyPostSaveKeys();
                            entity.ApplyPostOneSaveKeys();
                            entity.CommitPostSaves();
                            entity.CommitPostOneSaves();

                            tgEntitySavePacket packet;
                            packet.OriginalValues = entity.originalValues;
                            packet.CurrentValues = entity.currentValues;
                            packet.RowState = entity.rowState;
                            packet.ModifiedColumns = entity.tg.ModifiedColumns;
                            packet.Entity = entity;

                            request.CollectionSavePacket.Add(packet);
                        }

                        request.Columns = this.Meta.Columns;
                        request.SqlAccessType = sqlAccessType;

                        // Make the call to the DataProvider to physically commit the changes
                        provider = new tgDataProvider();
                        response = provider.esSaveDataTable(request, this.tg.Connection.ProviderSignature);
                        needToSendEvent = true;

                        if (!continueUpdateOnError && response.IsException)
                        {
                            throw response.Exception;
                        }
                    }

                    this.AcceptChanges();

                    scope.Complete();
                }
            }
            finally
            {
                this.saveNestingCount--;

                if (this.saveNestingCount == 0 && needToSendEvent == true)
                {
                    OnListChanged(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
                }
            }
        }

        void request_OnError(tgEntitySavePacket packet, string error)
        {
            tgEntity entity = packet.Entity as tgEntity;

            if (entity != null)
            {
                entity.rowError = error;
            }
        }

        #endregion

        #region LoadAll

        /// <summary>
        /// Loads all of the entities from the database.
        /// </summary>
        /// <remarks>
        /// The tgSqlAccessType is determined by the current connections 'sqlAccessType'
        /// as stored in the app/web config file.  The value is either DynamicSQL or StoredProcedure.
        /// </remarks>
        /// <returns>True if at least one entity was loaded, otherwise false.</returns>
        public override bool LoadAll()
        {
            if (this.tg.Connection.SqlAccessType == tgSqlAccessType.DynamicSQL)
                return this.GetDynamicQuery().Load();
            else
                return this.Load(tgQueryType.StoredProcedure, this.tg.spLoadAll);
        }

        /// <summary>
        /// Loads all of the entities from the database.
        /// </summary>
        /// <remarks>
        /// This overload lets you override the sqlAccessType stored in the app/web config
        /// file at runtime. The value is either DynamicSQL or StoredProcedure.
        /// </remarks>
        /// <example>
        /// <code>
        /// EmployeesCollection collection = new EmployeesCollection;
        /// Employees entity = new Employees;
        /// 
        /// collection.LoadAll(tgSqlAccessType.StoredProcedure);
        /// </code>
        /// </example>
        /// <param name="sqlAccessType">Either DynamicSQL or StoredProcedure.</param>
        /// <returns>True if at least one entity was loaded, otherwise false.</returns>
        public override bool LoadAll(tgSqlAccessType sqlAccessType)
        {
            if (sqlAccessType == tgSqlAccessType.DynamicSQL)
                return this.GetDynamicQuery().Load();
            else
                return this.Load(tgQueryType.StoredProcedure, this.tg.spLoadAll);
        }

        #endregion

        #region Load

        /// <summary>
        /// Called by the EntitySpaces Dynamic Query mechanism to load a collection.
        /// It can also be called from a method in your Custom entity.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public bool CustomLoad(string whereClause)
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///         sqlText = "SELECT [LastName], [DepartmentID], [HireDate] ";
        ///         sqlText += "FROM [Employees] ";
        ///         sqlText += whereClause;
        /// 
        ///         return this.Load(tgQueryType.Text, sqlText);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <returns>True if the entity was loaded.</returns>
        virtual protected bool Load(tgQueryType queryType, string query)
        {
            return Load(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Called by the EntitySpaces Dynamic Query mechanism to load a collection.
        /// It can also be called from a method in your Custom entity.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public bool CustomLoad()
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///			sqlText = "SELECT [LastName], [DepartmentID], [HireDate] ";
        ///			sqlText += "FROM [Employees] ";
        ///			sqlText += "WHERE [DepartmentID] = {0} ";
        ///			sqlText += "AND [EmployeeID] = {1}";
        /// 
        ///			return this.Load(tgQueryType.Text, sqlText, 1, 23);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>True if the entity was loaded.</returns>
        virtual protected bool Load(tgQueryType queryType, string query, params object[] parameters)
        {
            return Load(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Called by the EntitySpaces Dynamic Query mechanism to load a collection.
        /// It can also be called from a method in your Custom entity.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public bool CustomLoad(int empID)
        ///     {
        ///			string sqlText = String.Empty;
        ///			tgParameters esParams = new tgParameters();
        ///			esParams.Add("EmployeeID", empID);
        /// 
        ///			sqlText = "SELECT [LastName], [DepartmentID], [HireDate] ";
        ///			sqlText += "FROM [Employees] ";
        ///			sqlText += "WHERE [EmployeeID] = @EmployeeID";
        /// 
        ///			return this.Load(tgQueryType.Text, sqlText, esParams);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parms">A list of parameters.</param>
        /// <returns>True if the entity was loaded.</returns>
        virtual protected bool Load(tgQueryType queryType, string query, tgParameters parms)
        {
            bool loaded = false;
            try
            {
                tgDataRequest request = this.CreateRequest();

                request.Parameters = parms;
                request.QueryText = query;
                request.QueryType = queryType;

                tgDataProvider provider = new tgDataProvider();
                tgDataResponse response = provider.esLoadDataTable(request, this.tg.Connection.ProviderSignature);

                this.PopulateCollection(response.Table);
            }
            catch
            {
                throw;
            }
            finally
            {
                loaded = (this.Count > 0);
            }

            return loaded;
        }

        #endregion

        #region Graph Operations

        #region IsGraphDirty()

        /// <summary>
        /// Returns true if any entity or collection in the object graph returns true from thier 
        /// respect IsDirty property.
        /// </summary>
        public override bool IsGraphDirty
        {
            get
            {
                return !tgVisitor.Visit(this, IsGraphDirtyCallback);
            }
        }

        private bool IsGraphDirtyCallback(tgVisitParameters p)
        {
            bool isClean = true;

            if (p.Node.NodeType == tgVisitableNodeType.Entity)
            {
                if (p.Node.Entity.GetCollection() == null)
                {
                    isClean = !p.Node.Entity.tg.IsDirty;
                }
            }
            else
            {
                isClean = !p.Node.Collection.IsDirty;
            }

            if (!isClean)
            {
                p.ProcessChildren = false;
            }

            return isClean;
        }

        #endregion

        #region AcceptChangesGraph()

        /// <summary>
        /// Will call AcceptChanges on the entire object graph.
        /// </summary>
        public void AcceptChangesGraph()
        {
            tgVisitor.Visit(this, AcceptChangesGraphCallback);
        }

        private bool AcceptChangesGraphCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Entity)
            {
                if (p.Node.Entity.GetCollection() == null)
                {
                    p.Node.Entity.AcceptChanges();
                }
            }
            else
            {
                p.Node.Collection.AcceptChanges();
            }

            return true;
        }

        #endregion

        #region RejectChangesGraph()

        /// <summary>
        /// Will call RejectChanges() on the entire object graph.
        /// </summary>
        public void RejectChangesGraph()
        {
            tgVisitor.Visit(this, RejectChangesGraphCallback);
        }

        private bool RejectChangesGraphCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Entity)
            {
                if (p.Node.Entity.GetCollection() == null)
                {
                    p.Node.Entity.RejectChanges();
                }
            }
            else
            {
                p.Node.Collection.RejectChanges();
            }

            return true;
        }

        #endregion

        #region PruneGraph()

        /// <summary>
        /// Will eliminate all objects that are not modified from the object graph. If an object
        /// is not dirty but has children that are then it remains in the graph.
        /// </summary>
        public void PruneGraph()
        {
            tgVisitor.Visit(this, PruneGraphEnterCallback, PruneGraphExitCallback);
        }

        /// <summary>
        /// Will eliminate all objects that have the state or state(s) that you pass in. If you want to
        /// Prune all unmodified objects then PruneGraph() with no parameters, it's faster.
        /// </summary>
        /// <param name="statesToPrune">The states you wish to prune, can be many such as PruneGraph(tgDataRowState.Modified | tgDataRowState.Deleted)</param>
        public void PruneGraph(tgDataRowState statesToPrune)
        {
            tgVisitor.Visit(this, PruneGraphWithStateEnterCallback, PruneGraphWithStatExitCallback, statesToPrune);
        }

        private bool PruneGraphEnterCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Collection)
            {
                p.Node.UserState = new List<tgEntity>();
            }

            if (p.Node.NodeType == tgVisitableNodeType.Entity)
            {
                if (p.Node.Entity.GetCollection() == null)
                {
                    if (!p.Node.Entity.tg.IsDirty && !p.Node.Entity.tg.IsGraphDirty)
                    {
                        p.Node.SetValueToNull(p.Parent.Obj);
                    }
                }
                else if (!p.Node.Entity.tg.IsDirty && !p.Node.Entity.tg.IsGraphDirty)
                {
                    List<tgEntity> list = p.Parent.UserState as List<tgEntity>;
                    list.Add(p.Node.Entity);
                }
            }
            else
            {
                p.Node.UserState = new List<tgEntity>();

                if (!p.Node.Collection.IsDirty && !p.Node.Collection.IsGraphDirty)
                {
                     p.Node.SetValueToNull(p.Parent.Obj);
                }
            }

            return true;
        }

        private bool PruneGraphExitCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Collection)
            {
                tgEntityCollectionBase coll = p.Node.Collection as tgEntityCollectionBase;

                List<tgEntity> list = p.Node.UserState as List<tgEntity>;

                foreach (tgEntity entity in list)
                {
                    coll.RemoveEntity(entity);
                }

                if( coll.Count == 0 && !coll.Count.Equals(p.Root))
                {
                    p.Node.SetValueToNull(p.Parent.Obj);
                }
            }

            return true;
        }

        private bool PruneGraphWithStateEnterCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Collection)
            {
                p.Node.UserState = new List<tgEntity>();
            }

            if (p.Node.NodeType == tgVisitableNodeType.Entity)
            {
                if (p.Node.Entity.GetCollection() == null)
                {
                    if (MatchesState(p.Node.Entity.tg.RowState, (tgDataRowState)p.UserState))
                    {
                        p.Node.SetValueToNull(p.Parent.Obj);
                    }
                }
                else if (MatchesState(p.Node.Entity.tg.RowState, (tgDataRowState)p.UserState))
                {
                    List<tgEntity> list = p.Parent.UserState as List<tgEntity>;
                    list.Add(p.Node.Entity);
                }
            }
            else
            {
                p.Node.UserState = new List<tgEntity>();

                if (MatchesState(tgDataRowState.Deleted, (tgDataRowState)p.UserState))
                {
                    p.Node.Collection.ClearDeletedEntries();
                }

                bool canSetToNull = true;
                foreach (tgEntity entity in p.Node.Collection)
                {
                    if (!MatchesState(entity.tg.RowState, (tgDataRowState)p.UserState))
                    {
                        canSetToNull = false;
                        break;
                    }
                }

                if (canSetToNull)
                {
                    p.Node.SetValueToNull(p.Parent.Obj);
                }
            }

            return true;
        }

        private bool PruneGraphWithStatExitCallback(tgVisitParameters p)
        {
            if (p.Node.NodeType == tgVisitableNodeType.Collection)
            {
                tgEntityCollectionBase coll = p.Node.Collection as tgEntityCollectionBase;

                List<tgEntity> list = p.Node.UserState as List<tgEntity>;

                foreach (tgEntity entity in list)
                {
                    coll.RemoveEntity(entity);
                }

                if (coll.Count == 0 && !coll.Count.Equals(p.Root))
                {
                    p.Node.SetValueToNull(p.Parent.Obj);
                }
            }

            return true;
        }

        static private bool MatchesState(tgDataRowState theState, tgDataRowState statesToPrune)
        {
            return (theState == (statesToPrune & theState)) ? true : false;
        }

        #endregion

        #endregion

        #region Protected Helper routines

        static private tgParameters PackageParameters(params object[] parameters)
        {
            tgParameters esParams = null;

            int i = 0;
            string sIndex = String.Empty;
            string param = String.Empty;

            if (parameters != null)
            {
                esParams = new tgParameters();

                foreach (object o in parameters)
                {
                    sIndex = i.ToString();
                    param = "p" + sIndex;
                    esParams.Add(param, o);
                    i++;
                }
            }

            return esParams;
        }


        #region ExecuteNonQuery
        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName, int empID)
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///         sqlText = "UPDATE [Employees] ";
        ///         sqlText += "SET [LastName] = '" + newName + "' ";
        ///         sqlText += "WHERE [EmployeeID] = " + empID;
        /// 
        ///         return this.ExecuteNonQuery(tgQueryType.Text, sqlText);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(tgQueryType queryType, string query)
        {
            return ExecuteNonQuery(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName, int empID)
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///         sqlText = "UPDATE [Employees] ";
        ///         sqlText += "SET [LastName] = {0} ";
        ///         sqlText += "WHERE [EmployeeID] = {1}";
        /// 
        ///         return this.ExecuteNonQuery(tgQueryType.Text, sqlText, newName, empID);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(tgQueryType queryType, string query, params object[] parameters)
        {
            return ExecuteNonQuery(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName)
        ///     {
        ///			string sqlText = String.Empty;
        ///			tgParameters esParams = new tgParameters();
        ///			esParams.Add("FirstName", newName);
        ///			esParams.Add("LastName", "Doe");
        ///			esParams.Add("Salary", 27.53);
        /// 
        ///         sqlText = "UPDATE [Employees] ";
        ///			sqlText += "SET [FirstName] =  @FirstName ";
        ///			sqlText += "WHERE [LastName] = @LastName ";
        ///			sqlText += "AND [Salary] = @Salary";
        /// 
        ///         return this.ExecuteNonQuery(tgQueryType.Text, sqlText, esParams);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parms">A list of parameters. See <see cref="tgParameters"/>.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteNonQuery(request, this.tg.Connection.ProviderSignature);

            return response.RowsEffected;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate()
        ///     {
        ///			return this.ExecuteNonQuery(this.es.Schema, "MyStoredProc");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string schema, string storedProcedure)
        {
            return ExecuteNonQuery(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName, int empID)
        ///     {
        ///			return this.ExecuteNonQuery(this.es.Schema, "MyStoredProc", newName, empID);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteNonQuery(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName)
        ///     {
        ///			tgParameters esParams = new tgParameters();
        ///			esParams.Add("FirstName", newName);
        ///			esParams.Add("LastName", "Doe");
        ///			esParams.Add("Salary", 27.53);
        /// 
        ///			return this.ExecuteNonQuery(this.es.Schema, "MyStoredProc", esParams);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters. See <see cref="tgParameters"/>.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteNonQuery(request, this.tg.Connection.ProviderSignature);

            return response.RowsEffected;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate()
        ///     {
        ///			return this.ExecuteNonQuery(this.es.Catalog, this.es.Schema, "MyStoredProc");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string catalog, string schema, string storedProcedure)
        {
            return ExecuteNonQuery(catalog, schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName, int empID)
        ///     {
        ///			return this.ExecuteNonQuery(this.es.Catalog, this.es.Schema, "MyStoredProc", newName, empID);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string catalog, string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteNonQuery(catalog, schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteNonQuery.
        /// </remarks>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public int CustomUpdate(string newName)
        ///     {
        ///			tgParameters esParams = new tgParameters();
        ///			esParams.Add("FirstName", newName);
        ///			esParams.Add("LastName", "Doe");
        ///			esParams.Add("Salary", 27.53);
        /// 
        ///			return this.ExecuteNonQuery(this.es.Catalog, this.es.Schema, "MyStoredProc", esParams);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>The result of ExecuteNonQuery.</returns>
        protected internal int ExecuteNonQuery(string catalog, string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Catalog = catalog;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteNonQuery(request, this.tg.Connection.ProviderSignature);

            return response.RowsEffected;
        }

        #endregion

        #region ExecuteReader

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(tgQueryType queryType, string query)
        {
            return ExecuteReader(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(tgQueryType queryType, string query, params object[] parameters)
        {
            return ExecuteReader(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteReader(request, this.tg.Connection.ProviderSignature);

            return response.DataReader;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string schema, string storedProcedure)
        {
            return ExecuteReader(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteReader(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteReader(request, this.tg.Connection.ProviderSignature);

            return response.DataReader;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string catalog, string schema, string storedProcedure)
        {
            return ExecuteReader(catalog, schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string catalog, string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteReader(catalog, schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteReader.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteReader.</returns>
        protected internal IDataReader ExecuteReader(string catalog, string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Catalog = catalog;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteReader(request, this.tg.Connection.ProviderSignature);

            return response.DataReader;
        }

        #endregion

        #region ExecuteScalar

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(tgQueryType queryType, string query)
        {
            return ExecuteScalar(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(tgQueryType queryType, string query, params object[] parameters)
        {
            return ExecuteScalar(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg.Connection.ProviderSignature);

            return response.Scalar;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string schema, string storedProcedure)
        {
            return ExecuteScalar(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteScalar(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg.Connection.ProviderSignature);

            return response.Scalar;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string catalog, string schema, string storedProcedure)
        {
            return ExecuteScalar(catalog, schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string catalog, string schema, string storedProcedure, params object[] parameters)
        {
            return ExecuteScalar(catalog, schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        protected internal object ExecuteScalar(string catalog, string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Catalog = catalog;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg.Connection.ProviderSignature);

            return response.Scalar;
        }

        #endregion

        #region ExecuteScalar<T>

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(tgQueryType queryType, string query)
        {
            return (D)ExecuteScalar<D>(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(tgQueryType queryType, string query, params object[] parameters)
        {
            return (D)ExecuteScalar<D>(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg.Connection.ProviderSignature);

            if (response.Scalar == DBNull.Value)
            {
                response.Scalar = null;
            }

            return (D)response.Scalar;
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(string schema, string storedProcedure)
        {
            return (D)ExecuteScalar<D>(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(string schema, string storedProcedure, params object[] parameters)
        {
            return (D)ExecuteScalar<D>(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <remarks>
        /// See the .NET documentation for ExecuteScalar.
        /// </remarks>
        /// <example>
        /// See <see cref="ExecuteNonQuery"/> for examples,
        /// overloads, and parameters.
        /// </example>
        /// <returns>The result of ExecuteScalar.</returns>
        internal D ExecuteScalar<D>(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg.Connection.ProviderSignature);

            if (response.Scalar == DBNull.Value)
            {
                response.Scalar = null;
            }

            return (D)response.Scalar;
        }

        #endregion

        #region FillDataTable

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public DataTable CustomFillTable()
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///         sqlText = "SELECT * ";
        ///         sqlText += "FROM [Employees] ";
        ///         sqlText += "WHERE [LastName] = {0} ";
        ///         sqlText += "OR [LastName] = {1}";
        /// 
        ///         return this.FillDataTable(tgQueryType.Text, sqlText, "Doe", "Johnson");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(tgQueryType queryType, string query)
        {
            return FillDataTable(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(tgQueryType queryType, string query, params object[] parameters)
        {
            return FillDataTable(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parms">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.FillDataTable(request, this.tg.Connection.ProviderSignature);
            return response.Table;
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public DataTable CustomFillTable()
        ///     {
        ///			return this.FillDataTable(this.es.Schema, "MyStoredProc");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string schema, string storedProcedure)
        {
            return FillDataTable(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string schema, string storedProcedure, params object[] parameters)
        {
            return FillDataTable(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.FillDataTable(request, this.tg.Connection.ProviderSignature);

            return response.Table;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string catalog, string schema, string storedProcedure)
        {
            return FillDataTable(catalog, schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string catalog, string schema, string storedProcedure, params object[] parameters)
        {
            return FillDataTable(catalog, schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataTable containing the result set.</returns>
        protected internal DataTable FillDataTable(string catalog, string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Catalog = catalog;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.FillDataTable(request, this.tg.Connection.ProviderSignature);

            return response.Table;
        }

        #endregion

        #region FillDataSet

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public DataSet CustomFillDataSet()
        ///     {
        ///			string sqlText = String.Empty;
        /// 
        ///         sqlText = "SELECT * ";
        ///         sqlText += "FROM [Employees] ";
        ///         sqlText += "WHERE [LastName] = {0}";
        /// 
        ///         return this.FillDataSet(tgQueryType.Text, sqlText, "Doe");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(tgQueryType queryType, string query)
        {
            return FillDataSet(queryType, query, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(tgQueryType queryType, string query, params object[] parameters)
        {
            return FillDataSet(queryType, query, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="queryType">See <see cref="tgQueryType"/>.</param>
        /// <param name="query">Either the SQL for the Query or the name of a stored procedure.</param>
        /// <param name="parms">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(tgQueryType queryType, string query, tgParameters parms)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parms;
            request.QueryText = query;
            request.QueryType = queryType;

            tgDataProvider provider = new tgDataProvider();
            //tgDataResponse response = provider.FillDataTable(request, this.es.Connection.ProviderSignature);
            tgDataResponse response = provider.FillDataSet(request, this.tg.Connection.ProviderSignature);
            return response.DataSet;
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <example>
        /// <code>
        /// public partial class EmployeesCollection : esEmployeesCollection
        /// {
        ///     public DataSet CustomFillDataSet()
        ///     {
        ///			return this.FillDataSet(this.es.Schema, "MyStoredProc");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string schema, string storedProcedure)
        {
            return FillDataSet(schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string schema, string storedProcedure, params object[] parameters)
        {
            return FillDataSet(schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom entity.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="schema">See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.FillDataSet(request, this.tg.Connection.ProviderSignature);

            return response.DataSet;
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string catalog, string schema, string storedProcedure)
        {
            return FillDataSet(catalog, schema, storedProcedure, null as tgParameters);
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string catalog, string schema, string storedProcedure, params object[] parameters)
        {
            return FillDataSet(catalog, schema, storedProcedure, PackageParameters(parameters));
        }

        /// <summary>
        /// Can be called by a method in your Custom collection.
        /// This does not populate your entity.
        /// </summary>
        /// <param name="catalog">"Northwind" for example. See <see cref="IEntity.Catalog"/>.</param>
        /// <param name="schema">"dbo" for example. See <see cref="IEntity.Schema"/>.</param>
        /// <param name="storedProcedure">The name of a stored procedure.</param>
        /// <param name="parameters">A list of parameters.</param>
        /// <returns>A DataSet containing the result set.</returns>
        protected internal DataSet FillDataSet(string catalog, string schema, string storedProcedure, tgParameters parameters)
        {
            tgDataRequest request = this.CreateRequest();

            request.Parameters = parameters;
            request.Catalog = catalog;
            request.Schema = schema;
            request.QueryText = storedProcedure;
            request.QueryType = tgQueryType.StoredProcedure;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.FillDataSet(request, this.tg.Connection.ProviderSignature);

            return response.DataSet;
        }

        #endregion

        #endregion

        #region ListChanged Event Logic

        /// <summary>
        /// Called whenever the tgEntityCollection data is changed.
        /// </summary>
        public virtual event ListChangedEventHandler ListChanged
        {
            add
            {
                listChangedEventHandlerCount++;
                onListChangedEvent += value;

                if (entities.RaiseListChangedEvents == false)
                {
                    entities.RaiseListChangedEvents = true;
                }
            }
            remove
            {
                listChangedEventHandlerCount = Math.Max(0, --listChangedEventHandlerCount);
                onListChangedEvent -= value;

                if (listChangedEventHandlerCount == 0)
                {
                    entities.RaiseListChangedEvents = false;
                }
            }
        }

        private void OnListChanged(object sender, ListChangedEventArgs e)
        {
            ListChangedEventHandler handler = onListChangedEvent;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        [NonSerialized]
        [IgnoreDataMember]
        private ListChangedEventHandler onListChangedEvent;

        #endregion

        #region tgUpdateViewEventHandler

        internal virtual event tgUpdateViewEventHandler UpdateViewNotification
        {
            add
            {
                updateViewNotification += value;
            }
            remove
            {
                updateViewNotification -= value;
            }
        }

        internal override void OnUpdateViewNotification(object sender, ListChangedType changeType, tgEntity obj)
        {
            tgUpdateViewEventHandler handler = updateViewNotification;
            if (handler != null)
            {
                handler(this, changeType, obj);
            }
        }

        [NonSerialized]
        [IgnoreDataMember]
        private tgUpdateViewEventHandler updateViewNotification;

#endregion 

        #region ICommittable Members

        bool ICommittable.Commit()
        {
            AcceptChanges();
            return true;
        }

        #endregion

        /// <summary>
        /// This is used by the Hierarchical Code.
        /// </summary>
        /// 
        [NonSerialized, XmlIgnore]
        public Dictionary<string, object> fks = new Dictionary<string, object>();

        [NonSerialized]
        private int saveNestingCount = 0;

        [NonSerialized]
        [IgnoreDataMember]
        private int listChangedEventHandlerCount;


        internal BindingList<T> entities = new BindingList<T>();
        internal BindingList<T> entitiesFilterBackup;

        internal IList<T> deletedEntities;

        [NonSerialized]
        internal IQueryable<T> currentFilter;

        [NonSerialized]
        private T objectCreator = new T();

        [NonSerialized]
        static internal tgExtendedPropertyAttribute extendedPropertyAttribute = new tgExtendedPropertyAttribute();

        [NonSerialized]
        static internal tgHierarchicalPropertyAttribute hierarchicalPropertyAttribute = new tgHierarchicalPropertyAttribute();
    }
}
