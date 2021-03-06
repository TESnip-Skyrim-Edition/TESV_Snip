﻿/*
 * DataSourceAdapter - A helper class that translates DataSource events for an ObjectListView
 *
 * Author: Phillip Piper
 * Date: 20/09/2010 7:42 AM
 *
 * Change log:
 * 2010-09-20   JPP  - Initial version
 * 
 * Copyright (C) 2010 Phillip Piper
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 * If you wish to use this code in a closed source application, please contact phillip_piper@bigfoot.com.
 */

using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
    /// <summary>
    /// A helper class that translates DataSource events for an ObjectListView
    /// </summary>
    public class DataSourceAdapter : IDisposable
    {
        #region Life and death

        /// <summary>
        /// Make a DataSourceAdapter
        /// </summary>
        public DataSourceAdapter(ObjectListView olv)
        {
            Debug.Assert(olv != null);

            ListView = olv;
            BindListView(ListView);
        }

        /// <summary>
        /// Release all the resources used by this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release all the resources used by this instance
        /// </summary>
        public void Dispose(bool all)
        {
            UnbindListView(ListView);
            UnbindDataSource();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Get or set the DataSource that will be displayed in this list view.
        /// </summary>
        public virtual Object DataSource
        {
            get { return dataSource; }
            set
            {
                dataSource = value;
                RebindDataSource(true);
            }
        }

        private Object dataSource;

        /// <summary>
        /// Gets or sets the name of the list or table in the data source for which the DataListView is displaying data.
        /// </summary>
        /// <remarks>If the data source is not a DataSet or DataViewManager, this property has no effect</remarks>
        public virtual string DataMember
        {
            get { return dataMember; }
            set
            {
                if (dataMember != value)
                {
                    dataMember = value;
                    RebindDataSource();
                }
            }
        }

        private string dataMember = "";

        /// <summary>
        /// Gets the ObjectListView upon which this adaptor will operate
        /// </summary>
        public ObjectListView ListView { get; internal set; }

        #endregion

        #region Implementation properties

        /// <summary>
        /// Gets or sets the currency manager which is handling our binding context
        /// </summary>
        protected CurrencyManager CurrencyManager { get; set; }

        #endregion

        #region Binding and unbinding

        private void BindListView(ObjectListView listView)
        {
            if (listView == null)
                return;

            listView.Freezing += listView_Freezing;
            listView.SelectedIndexChanged += listView_SelectedIndexChanged;
            listView.BindingContextChanged += listView_BindingContextChanged;
        }

        private void UnbindListView(ObjectListView listView)
        {
            if (listView == null)
                return;

            listView.Freezing -= listView_Freezing;
            listView.SelectedIndexChanged -= listView_SelectedIndexChanged;
            listView.BindingContextChanged -= listView_BindingContextChanged;
        }

        private void BindDataSource()
        {
            if (CurrencyManager == null)
                return;

            CurrencyManager.MetaDataChanged += currencyManager_MetaDataChanged;
            CurrencyManager.PositionChanged += currencyManager_PositionChanged;
            CurrencyManager.ListChanged += currencyManager_ListChanged;
        }

        private void UnbindDataSource()
        {
            if (CurrencyManager == null)
                return;

            CurrencyManager.MetaDataChanged -= currencyManager_MetaDataChanged;
            CurrencyManager.PositionChanged -= currencyManager_PositionChanged;
            CurrencyManager.ListChanged -= currencyManager_ListChanged;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Our data source has changed. Figure out how to handle the new source
        /// </summary>
        protected virtual void RebindDataSource()
        {
            RebindDataSource(false);
        }

        /// <summary>
        /// Our data source has changed. Figure out how to handle the new source
        /// </summary>
        protected virtual void RebindDataSource(bool forceDataInitialization)
        {
            CurrencyManager tempCurrencyManager = null;
            if (ListView != null && ListView.BindingContext != null && DataSource != null)
            {
                tempCurrencyManager = (CurrencyManager) ListView.BindingContext[DataSource, DataMember];
            }

            // Has our currency manager changed?
            if (CurrencyManager != tempCurrencyManager)
            {
                UnbindDataSource();
                CurrencyManager = tempCurrencyManager;
                BindDataSource();

                // Our currency manager has changed so we have to initialize a new data source
                forceDataInitialization = true;
            }

            if (forceDataInitialization)
                InitializeDataSource();
        }

        /// <summary>
        /// The data source for this control has changed. Reconfigure the control for the new source
        /// </summary>
        protected virtual void InitializeDataSource()
        {
            if (ListView.Frozen || CurrencyManager == null)
                return;

            CreateColumnsFromSource();
            CreateMissingAspectGettersAndPutters();
            SetListContents();
            InitializeColumnWidths();
        }

        /// <summary>
        /// Take the contents of the currently bound list and put them into the control
        /// </summary>
        protected virtual void SetListContents()
        {
            ListView.Objects = CurrencyManager.List;
        }

        /// <summary>
        /// Set up any automatically initialized column widths
        /// </summary>
        protected virtual void InitializeColumnWidths()
        {
            // If we are supposed to resize to content, but there is no content, resize to
            // the header size instead.
            ColumnHeaderAutoResizeStyle resizeToContentStyle = ColumnHeaderAutoResizeStyle.ColumnContent;
            if (ListView.GetItemCount() == 0)
                resizeToContentStyle = ColumnHeaderAutoResizeStyle.HeaderSize;
            foreach (ColumnHeader column in ListView.Columns)
            {
                if (column.Width == 0)
                    ListView.AutoResizeColumn(column.Index, resizeToContentStyle);
                else if (column.Width == -1)
                    ListView.AutoResizeColumn(column.Index, ColumnHeaderAutoResizeStyle.HeaderSize);
            }
        }

        /// <summary>
        /// Create columns for the listview based on what properties are available in the data source
        /// </summary>
        /// <remarks>
        /// <para>This method will not replace existing columns.</para>
        /// </remarks>
        protected virtual void CreateColumnsFromSource()
        {
            if (CurrencyManager == null || ListView.AllColumns.Count != 0)
                return;

            // Don't generate any columns in design mode. If we do, the user will see them,
            // but the Designer won't know about them and won't persist them, which is very confusing
            if (ListView.IsDesignMode)
                return;

            PropertyDescriptorCollection properties = CurrencyManager.GetItemProperties();
            if (properties.Count == 0)
                return;

            for (int i = 0; i < properties.Count; i++)
            {
                PropertyDescriptor property = properties[i];

                // Relationships to other tables turn up as IBindibleLists. Don't make columns to show them.
                // CHECK: Is this always true? What other things could be here? Constraints? Triggers?
                if (property.PropertyType == typeof (IBindingList))
                    continue;

                // Create a column
                var column = new OLVColumn(DisplayNameToColumnTitle(property.DisplayName), property.Name);
                column.IsEditable = !property.IsReadOnly;
                column.Width = CalculateColumnWidth(property);
                column.LastDisplayIndex = i;
                ConfigureColumn(column, property);

                // Add it to our list
                ListView.AllColumns.Add(column);
            }

            if (ListView.AllColumns.Exists(delegate(OLVColumn x) { return x.CheckBoxes; }))
                ListView.SetupSubItemCheckBoxes();

            ListView.RebuildColumns();
        }

        /// <summary>
        /// Calculate how wide the column for the given property should be
        /// when it is first created. 
        /// </summary>
        /// <param name="property">The property for which a column is being created</param>
        /// <returns>The initial width of the column. 0 means auto size to contents. -1 means auto
        /// size to column header.</returns>
        protected virtual int CalculateColumnWidth(PropertyDescriptor property)
        {
            return 0; // Resize to data contents
        }

        /// <summary>
        /// Convert the given property display name into a column title
        /// </summary>
        /// <param name="displayName">The display name of the property</param>
        /// <returns>The title of the column</returns>
        protected virtual string DisplayNameToColumnTitle(string displayName)
        {
            string title = displayName.Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title);
        }

        /// <summary>
        /// Configure the given column to show the given property.
        /// The title and aspect name of the column are already filled in.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="property"></param>
        protected virtual void ConfigureColumn(OLVColumn column, PropertyDescriptor property)
        {
            if (property.PropertyType == typeof (bool) || property.PropertyType == typeof (CheckState))
            {
                column.TextAlign = HorizontalAlignment.Center;
                column.Width = 32;
                column.CheckBoxes = true;

                if (property.PropertyType == typeof (CheckState))
                    column.TriStateCheckBoxes = true;
            }

            // If our column is a BLOB, it could be an image, so assign a renderer to draw it.
            // CONSIDER: Is this a common enough case to warrant this code?
            if (property.PropertyType == typeof (Byte[]))
                column.Renderer = new ImageRenderer();
        }

        /// <summary>
        /// Generate aspect getters and putters for any columns that are missing them (and for which we have
        /// enough information to actually generate a getter)
        /// </summary>
        protected virtual void CreateMissingAspectGettersAndPutters()
        {
            foreach (OLVColumn x in ListView.AllColumns)
            {
                OLVColumn column = x; // stack based variable accessible from closures
                if (column.AspectGetter == null && !String.IsNullOrEmpty(column.AspectName))
                {
                    column.AspectGetter = delegate(object row)
                                              {
                                                  // In most cases, rows will be DataRowView objects
                                                  var drv = row as DataRowView;
                                                  if (drv == null)
                                                      return column.GetAspectByName(row);
                                                  return (drv.Row.RowState == DataRowState.Detached)
                                                             ? null
                                                             : drv[column.AspectName];
                                              };
                }
                if (column.IsEditable && column.AspectPutter == null && !String.IsNullOrEmpty(column.AspectName))
                {
                    column.AspectPutter = delegate(object row, object newValue)
                                              {
                                                  // In most cases, rows will be DataRowView objects
                                                  var drv = row as DataRowView;
                                                  if (drv == null)
                                                      column.PutAspectByName(row, newValue);
                                                  else
                                                      drv[column.AspectName] = newValue;
                                              };
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// CurrencyManager ListChanged event handler.
        /// Deals with fine-grained changes to list items.
        /// </summary>
        /// <remarks>
        /// It's actually difficult to deal with these changes in a fine-grained manner.
        /// If our listview is grouped, then any change may make a new group appear or
        /// an old group disappear. It is rarely enough to simply update the affected row.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void currencyManager_ListChanged(object sender, ListChangedEventArgs e)
        {
            Debug.Assert(sender == CurrencyManager);

            // Ignore changes make while frozen, since we will do a complete rebuild when we unfreeze
            if (ListView.Frozen)
                return;

            //System.Diagnostics.Debug.WriteLine(e.ListChangedType);
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            switch (e.ListChangedType)
            {
                case ListChangedType.Reset:
                    HandleListChanged_Reset(e);
                    break;

                case ListChangedType.ItemChanged:
                    HandleListChanged_ItemChanged(e);
                    break;

                case ListChangedType.ItemAdded:
                    HandleListChanged_ItemAdded(e);
                    break;

                    // An item has gone away.
                case ListChangedType.ItemDeleted:
                    HandleListChanged_ItemDeleted(e);
                    break;

                    // An item has changed its index.
                case ListChangedType.ItemMoved:
                    HandleListChanged_ItemMoved(e);
                    break;

                    // Something has changed in the metadata.
                    // CHECK: When are these events actually fired?
                case ListChangedType.PropertyDescriptorAdded:
                case ListChangedType.PropertyDescriptorChanged:
                case ListChangedType.PropertyDescriptorDeleted:
                    HandleListChanged_MetadataChanged(e);
                    break;
            }
            //sw.Stop();
            //System.Diagnostics.Debug.WriteLine(String.Format("Processing {0} event on {1} rows took {2}ms", e.ListChangedType, this.ListView.GetItemCount(), sw.ElapsedMilliseconds));
        }

        /// <summary>
        /// Handle PropertyDescriptor* events
        /// </summary>
        /// <param name="e"></param>
        private void HandleListChanged_MetadataChanged(ListChangedEventArgs e)
        {
            InitializeDataSource();
        }

        /// <summary>
        /// Handle ItemMoved event
        /// </summary>
        /// <param name="e"></param>
        private void HandleListChanged_ItemMoved(ListChangedEventArgs e)
        {
            // When is this actually triggered?
            InitializeDataSource();
        }

        /// <summary>
        /// Handle the ItemDeleted event
        /// </summary>
        /// <param name="e"></param>
        private void HandleListChanged_ItemDeleted(ListChangedEventArgs e)
        {
            InitializeDataSource();
        }

        /// <summary>
        /// Handle an ItemAdded event.
        /// </summary>
        /// <param name="e"></param>
        private void HandleListChanged_ItemAdded(ListChangedEventArgs e)
        {
            // We get this event twice if certain grid controls are used to add a new row to a
            // datatable: once when the editing of a new row begins, and once again when that
            // editing commits. (If the user cancels the creation of the new row, we never see
            // the second creation.) We detect this by seeing if this is a view on a row in a
            // DataTable, and if it is, testing to see if it's a new row under creation.

            Object newRow = CurrencyManager.List[e.NewIndex];
            var drv = newRow as DataRowView;
            if (drv == null || !drv.IsNew)
            {
                // Either we're not dealing with a view on a data table, or this is the commit
                // notification. Either way, this is the final notification, so we want to
                // handle the new row now!
                InitializeDataSource();
            }
        }

        /// <summary>
        /// Handle the Reset event
        /// </summary>
        /// <param name="e"></param>
        private void HandleListChanged_Reset(ListChangedEventArgs e)
        {
            // The whole list has changed utterly, so reload it.
            InitializeDataSource();
        }

        /// <summary>
        /// Handle ItemChanged event. This is triggered when a single item
        /// has changed, so just refresh that one item.
        /// </summary>
        /// <param name="e"></param>
        /// <remarks>Even in this simple case, we should probably rebuild the list.
        /// For example, the change could put the item into its own new group.</remarks>
        private void HandleListChanged_ItemChanged(ListChangedEventArgs e)
        {
            // A single item has changed, so just refresh that.

            Object changedRow = CurrencyManager.List[e.NewIndex];
            ListView.RefreshObject(changedRow);
        }

        /// <summary>
        /// The CurrencyManager calls this if the data source looks
        /// different. We just reload everything.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// CHECK: Do we need this if we are handle ListChanged metadata events?
        /// </remarks>
        protected virtual void currencyManager_MetaDataChanged(object sender, EventArgs e)
        {
            InitializeDataSource();
        }

        /// <summary>
        /// Called by the CurrencyManager when the currently selected item
        /// changes. We update the ListView selection so that we stay in sync
        /// with any other controls bound to the same source.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void currencyManager_PositionChanged(object sender, EventArgs e)
        {
            int index = CurrencyManager.Position;

            // Make sure the index is sane (-1 pops up from time to time)
            if (index < 0 || index >= ListView.GetItemCount())
                return;

            // Avoid recursion. If we are currently changing the index, don't
            // start the process again.
            if (isChangingIndex)
                return;

            try
            {
                isChangingIndex = true;

                // We can't use the index directly, since our listview may be sorted
                ListView.SelectedObject = CurrencyManager.List[index];

                // THINK: Do we always want to bring it into view?
                if (ListView.SelectedIndices.Count > 0)
                    ListView.EnsureVisible(ListView.SelectedIndices[0]);
            }
            finally
            {
                isChangingIndex = false;
            }
        }

        private bool isChangingIndex;

        #endregion

        #region ObjectListView event handlers

        /// <summary>
        /// Handle the selection changing in our ListView.
        /// We need to tell our currency manager about the new position.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Prevent recursion
            if (isChangingIndex)
                return;

            // If we are bound to a datasource, and only one item is selected,
            // tell the currency manager which item is selected.
            if (ListView.SelectedIndices.Count == 1 && CurrencyManager != null)
            {
                try
                {
                    isChangingIndex = true;

                    // We can't use the selectedIndex directly, since our listview may be sorted.
                    // So we have to find the index of the selected object within the original list.
                    CurrencyManager.Position = CurrencyManager.List.IndexOf(ListView.SelectedObject);
                }
                finally
                {
                    isChangingIndex = false;
                }
            }
        }

        /// <summary>
        /// Handle the frozenness of our ListView changing. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void listView_Freezing(object sender, FreezeEventArgs e)
        {
            if (!alreadyFreezing && e.FreezeLevel == 0)
            {
                try
                {
                    alreadyFreezing = true;
                    RebindDataSource(true);
                }
                finally
                {
                    alreadyFreezing = false;
                }
            }
        }

        private bool alreadyFreezing;

        /// <summary>
        /// Handle a change to the BindingContext of our ListView.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void listView_BindingContextChanged(object sender, EventArgs e)
        {
            RebindDataSource(false);
        }

        #endregion
    }
}