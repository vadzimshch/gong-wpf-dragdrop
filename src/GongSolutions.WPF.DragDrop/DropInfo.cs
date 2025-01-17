﻿using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using GongSolutions.Wpf.DragDrop.Utilities;
using JetBrains.Annotations;

namespace GongSolutions.Wpf.DragDrop
{
    /// <summary>
    /// Holds information about a the target of a drag drop operation.
    /// </summary>
    /// 
    /// <remarks>
    /// The <see cref="DropInfo"/> class holds all of the framework's information about the current 
    /// target of a drag. It is used by <see cref="IDropTarget.DragOver"/> method to determine whether 
    /// the current drop target is valid, and by <see cref="IDropTarget.Drop"/> to perform the drop.
    /// </remarks>
    public class DropInfo : IDropInfo
    {
        private ItemsControl itemParent = null;
        private UIElement item = null;

        /// <summary>
        /// Initializes a new instance of the DropInfo class.
        /// </summary>
        /// 
        /// <param name="sender">
        /// The sender of the drag event.
        /// </param>
        /// 
        /// <param name="e">
        /// The drag event.
        /// </param>
        /// 
        /// <param name="dragInfo">
        /// Information about the source of the drag, if the drag came from within the framework.
        /// </param>
        /// 
        /// <param name="eventType">
        /// The type of the underlying event (tunneled or bubbled).
        /// </param>
        public DropInfo(object sender, DragEventArgs e, [CanBeNull] DragInfo dragInfo, EventType eventType)
        {
            this.DragInfo = dragInfo;
            this.KeyStates = e.KeyStates;
            this.EventType = eventType;
            var dataFormat = dragInfo?.DataFormat;
            this.Data = dataFormat != null && e.Data.GetDataPresent(dataFormat.Name) ? e.Data.GetData(dataFormat.Name) : e.Data;

            this.VisualTarget = sender as UIElement;
            // if there is no drop target, find another
            if (!this.VisualTarget.IsDropTarget())
            {
                // try to find next element
                var element = this.VisualTarget.TryGetNextAncestorDropTargetElement();
                if (element != null)
                {
                    this.VisualTarget = element;
                }
            }

            // try find ScrollViewer
            var dropTargetScrollViewer = DragDrop.GetDropTargetScrollViewer(this.VisualTarget);
            if (dropTargetScrollViewer != null)
            {
                this.TargetScrollViewer = dropTargetScrollViewer;
            }
            else if (this.VisualTarget is TabControl)
            {
                var tabPanel = this.VisualTarget.GetVisualDescendent<TabPanel>();
                this.TargetScrollViewer = tabPanel?.GetVisualAncestor<ScrollViewer>();
            }
            else
            {
                this.TargetScrollViewer = this.VisualTarget?.GetVisualDescendent<ScrollViewer>();
            }

            this.TargetScrollingMode = this.VisualTarget != null ? DragDrop.GetDropScrollingMode(this.VisualTarget) : ScrollingMode.Both;

            // visual target can be null, so give us a point...
            this.DropPosition = this.VisualTarget != null ? e.GetPosition(this.VisualTarget) : new Point();

            if (this.VisualTarget is TabControl)
            {
                if (!HitTestUtilities.HitTest4Type<TabPanel>(this.VisualTarget, this.DropPosition))
                {
                    return;
                }
            }

            if (this.VisualTarget is ItemsControl)
            {
                var itemsControl = (ItemsControl)this.VisualTarget;
                //System.Diagnostics.Debug.WriteLine(">>> Name = {0}", itemsControl.Name);
                // get item under the mouse
                item = itemsControl.GetItemContainerAt(this.DropPosition);
                var directlyOverItem = item != null;

                this.TargetGroup = itemsControl.FindGroup(this.DropPosition);
                this.VisualTargetOrientation = itemsControl.GetItemsPanelOrientation();
                this.VisualTargetFlowDirection = itemsControl.GetItemsPanelFlowDirection();

                if (item == null)
                {
                    // ok, no item found, so maybe we can found an item at top, left, right or bottom
                    item = itemsControl.GetItemContainerAt(this.DropPosition, this.VisualTargetOrientation);
                    directlyOverItem = DropPosition.DirectlyOverElement(this.item, itemsControl);
                }

                if (item == null && this.TargetGroup != null && this.TargetGroup.IsBottomLevel)
                {
                    var itemData = this.TargetGroup.Items.FirstOrDefault();
                    if (itemData != null)
                    {
                        item = itemsControl.ItemContainerGenerator.ContainerFromItem(itemData) as UIElement;
                        directlyOverItem = DropPosition.DirectlyOverElement(this.item, itemsControl);
                    }
                }

                if (item != null)
                {
                    itemParent = ItemsControl.ItemsControlFromItemContainer(item);
                    this.VisualTargetOrientation = itemParent.GetItemsPanelOrientation();
                    this.VisualTargetFlowDirection = itemParent.GetItemsPanelFlowDirection();

                    this.InsertIndex = itemParent.ItemContainerGenerator.IndexFromContainer(item);
                    this.TargetCollection = itemParent.ItemsSource ?? itemParent.Items;

                    var tvItem = item as TreeViewItem;

                    if (directlyOverItem || tvItem != null)
                    {
                        this.VisualTargetItem = item;
                        this.TargetItem = itemParent.ItemContainerGenerator.ItemFromContainer(item);
                    }

                    var expandedTVItem = tvItem != null && tvItem.HasHeader && tvItem.HasItems && tvItem.IsExpanded;
                    var itemRenderSize = expandedTVItem ? tvItem.GetHeaderSize() : item.RenderSize;

                    if (this.VisualTargetOrientation == Orientation.Vertical)
                    {
                        var currentYPos = e.GetPosition(item).Y;
                        var targetHeight = itemRenderSize.Height;

                        var topGap = targetHeight * 0.25;
                        var bottomGap = targetHeight * 0.75;
                        if (currentYPos > targetHeight / 2)
                        {
                            if (expandedTVItem && (currentYPos < topGap || currentYPos > bottomGap))
                            {
                                this.VisualTargetItem = tvItem.ItemContainerGenerator.ContainerFromIndex(0) as UIElement;
                                this.TargetItem = this.VisualTargetItem != null ? tvItem.ItemContainerGenerator.ItemFromContainer(this.VisualTargetItem) : null;
                                this.TargetCollection = tvItem.ItemsSource ?? tvItem.Items;
                                this.InsertIndex = 0;
                                this.InsertPosition = RelativeInsertPosition.BeforeTargetItem;
                            }
                            else
                            {
                                this.InsertIndex++;
                                this.InsertPosition = RelativeInsertPosition.AfterTargetItem;
                            }
                        }
                        else
                        {
                            this.InsertPosition = RelativeInsertPosition.BeforeTargetItem;
                        }

                        if (currentYPos > topGap && currentYPos < bottomGap)
                        {
                            if (tvItem != null)
                            {
                                this.TargetCollection = tvItem.ItemsSource ?? tvItem.Items;
                                this.InsertIndex = this.TargetCollection != null ? this.TargetCollection.OfType<object>().Count() : 0;
                            }

                            this.InsertPosition |= RelativeInsertPosition.TargetItemCenter;
                        }
                        //System.Diagnostics.Debug.WriteLine("==> DropInfo: pos={0}, idx={1}, Y={2}, Item={3}", this.InsertPosition, this.InsertIndex, currentYPos, item);
                    }
                    else
                    {
                        var currentXPos = e.GetPosition(item).X;
                        var targetWidth = itemRenderSize.Width;

                        if (this.VisualTargetFlowDirection == FlowDirection.RightToLeft)
                        {
                            if (currentXPos > targetWidth / 2)
                            {
                                this.InsertPosition = RelativeInsertPosition.BeforeTargetItem;
                            }
                            else
                            {
                                this.InsertIndex++;
                                this.InsertPosition = RelativeInsertPosition.AfterTargetItem;
                            }
                        }
                        else if (this.VisualTargetFlowDirection == FlowDirection.LeftToRight)
                        {
                            if (currentXPos > targetWidth / 2)
                            {
                                this.InsertIndex++;
                                this.InsertPosition = RelativeInsertPosition.AfterTargetItem;
                            }
                            else
                            {
                                this.InsertPosition = RelativeInsertPosition.BeforeTargetItem;
                            }
                        }

                        if (currentXPos > targetWidth * 0.25 && currentXPos < targetWidth * 0.75)
                        {
                            if (tvItem != null)
                            {
                                this.TargetCollection = tvItem.ItemsSource ?? tvItem.Items;
                                this.InsertIndex = this.TargetCollection != null ? this.TargetCollection.OfType<object>().Count() : 0;
                            }

                            this.InsertPosition |= RelativeInsertPosition.TargetItemCenter;
                        }
                        //System.Diagnostics.Debug.WriteLine("==> DropInfo: pos={0}, idx={1}, X={2}, Item={3}", this.InsertPosition, this.InsertIndex, currentXPos, item);
                    }
                }
                else
                {
                    this.TargetCollection = itemsControl.ItemsSource ?? itemsControl.Items;
                    this.InsertIndex = itemsControl.Items.Count;
                    //System.Diagnostics.Debug.WriteLine("==> DropInfo: pos={0}, item=NULL, idx={1}", this.InsertPosition, this.InsertIndex);
                }
            }
            else
            {
                this.VisualTargetItem = this.VisualTarget;
            }
        }

        /// <inheritdoc />
        public object Data { get; internal set; }

        /// <inheritdoc />
        public IDragInfo DragInfo { get; private set; }

        /// <inheritdoc />
        public Point DropPosition { get; private set; }

        /// <inheritdoc />
        public Type DropTargetAdorner { get; set; }

        /// <inheritdoc />
        public DragDropEffects Effects { get; set; }

        /// <inheritdoc />
        public int InsertIndex { get; private set; }

        /// <inheritdoc />
        public int UnfilteredInsertIndex
        {
            get
            {
                var insertIndex = this.InsertIndex;
                if (itemParent != null)
                {
                    var itemSourceAsList = itemParent.ItemsSource.TryGetList();
                    if (itemSourceAsList != null && itemParent.Items != null && itemParent.Items.Count != itemSourceAsList.Count)
                    {
                        if (insertIndex >= 0 && insertIndex < itemParent.Items.Count)
                        {
                            var indexOf = itemSourceAsList.IndexOf(itemParent.Items[insertIndex]);
                            if (indexOf >= 0)
                            {
                                return indexOf;
                            }
                        }
                        else if (itemParent.Items.Count > 0 && insertIndex == itemParent.Items.Count)
                        {
                            var indexOf = itemSourceAsList.IndexOf(itemParent.Items[insertIndex - 1]);
                            if (indexOf >= 0)
                            {
                                return indexOf + 1;
                            }
                        }
                    }
                }

                return insertIndex;
            }
        }

        /// <inheritdoc />
        public IEnumerable TargetCollection { get; private set; }

        /// <inheritdoc />
        public object TargetItem { get; private set; }

        /// <inheritdoc />
        public CollectionViewGroup TargetGroup { get; private set; }

        /// <summary>
        /// Gets the ScrollViewer control for the visual target.
        /// </summary>
        public ScrollViewer TargetScrollViewer { get; private set; }

        /// <summary>
        /// Gets or Sets the ScrollingMode for the drop action.
        /// </summary>
        public ScrollingMode TargetScrollingMode { get; set; }

        /// <inheritdoc />
        public UIElement VisualTarget { get; private set; }

        /// <inheritdoc />
        public UIElement VisualTargetItem { get; private set; }

        /// <inheritdoc />
        public Orientation VisualTargetOrientation { get; private set; }

        /// <inheritdoc />
        public FlowDirection VisualTargetFlowDirection { get; private set; }

        /// <inheritdoc />
        public string DestinationText { get; set; }

        /// <inheritdoc />
        public string EffectText { get; set; }

        /// <inheritdoc />
        public RelativeInsertPosition InsertPosition { get; private set; }

        /// <inheritdoc />
        public DragDropKeyStates KeyStates { get; private set; }

        /// <inheritdoc />
        public bool NotHandled { get; set; }

        /// <inheritdoc />
        public bool IsSameDragDropContextAsSource
        {
            get
            {
                // Check if DragInfo stuff exists
                if (this.DragInfo?.VisualSource is null)
                {
                    return true;
                }

                // A target should be exists
                if (this.VisualTarget is null)
                {
                    return true;
                }

                // Source element has a drag context constraint, we need to check the target property matches.
                var sourceContext = DragDrop.GetDragDropContext(this.DragInfo.VisualSource);
                var targetContext = DragDrop.GetDragDropContext(this.VisualTarget);

                return string.Equals(sourceContext, targetContext)
                       || string.IsNullOrEmpty(targetContext);
            }
        }

        /// <inheritdoc />
        public EventType EventType { get; }
    }

    [Flags]
    public enum RelativeInsertPosition
    {
        None = 0,
        BeforeTargetItem = 1,
        AfterTargetItem = 2,
        TargetItemCenter = 4
    }
}