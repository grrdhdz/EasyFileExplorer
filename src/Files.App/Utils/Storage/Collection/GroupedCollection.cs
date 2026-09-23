// Copyright (c) Files Community
// Licensed under the MIT License.

[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(Files.App.Utils.Storage.GroupedCollection<Files.App.Utils.ListedItem>))]

namespace Files.App.Utils.Storage
{
	[WinRT.GeneratedWinRTExposedType]
	public sealed partial class GroupedCollection<T> : BulkConcurrentObservableCollection<T>, IGroupedCollectionHeader
	{
		private GroupedHeaderViewModel? model;
		public GroupedHeaderViewModel Model
		{
			get => model ?? throw new InvalidOperationException("The group header model has not been initialized.");
			set => model = value;
		}

		public GroupedCollection(IEnumerable<T> items) : base(items)
		{
			AddEvents();
		}

		public GroupedCollection(string key) : base()
		{
			AddEvents();
			Model = new GroupedHeaderViewModel()
			{
				Key = key,
				Text = key,
			};
		}

		public GroupedCollection(string key, string text) : base()
		{
			AddEvents();
			Model = new GroupedHeaderViewModel()
			{
				Key = key,
				Text = text,
			};
		}

		private void AddEvents()
		{
			PropertyChanged += GroupedCollection_PropertyChanged;
		}

		private void GroupedCollection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Count))
			{
				var count = Count + (collapsedItems?.Count ?? 0);
				Model.CountText = string.Format(
					count > 1
						? Strings.GroupItemsCount_Plural.GetLocalizedResource()
						: Strings.GroupItemsCount_Singular.GetLocalizedResource(),
					count);
			}
		}

		// Collapsing

		private List<T>? collapsedItems;

		/// <summary>
		/// Removes all items from the collection so the list renders only the group header.
		/// The items are stashed and can be restored with <see cref="Expand"/>.
		/// </summary>
		public void Collapse()
		{
			if (collapsedItems is not null)
				return;

			collapsedItems = new List<T>(this);
			base.Clear();
			Model.IsCollapsed = true;
		}

		/// <summary>
		/// Restores the stashed items after a collapse.
		/// </summary>
		public void Expand()
		{
			if (collapsedItems is null)
				return;

			var items = collapsedItems;
			collapsedItems = null;
			base.AddRange(items);
			Model.IsCollapsed = false;
		}

		// While collapsed, mutations are redirected to the stash so the group stays empty
		// (e.g. new files joining a collapsed group stay hidden until it is expanded).

		public override void Add(T? item)
		{
			if (collapsedItems is not null)
			{
				if (item is not null)
					collapsedItems.Add(item);
				return;
			}
			base.Add(item);
		}

		public override void Insert(int index, T? item)
		{
			if (collapsedItems is not null)
			{
				if (item is not null)
					collapsedItems.Insert(Math.Min(index, collapsedItems.Count), item);
				return;
			}
			base.Insert(index, item);
		}

		public override void AddRange(IEnumerable<T> items)
		{
			if (collapsedItems is not null)
			{
				collapsedItems.AddRange(items);
				return;
			}
			base.AddRange(items);
		}

		public override void InsertRange(int index, IEnumerable<T> items)
		{
			if (collapsedItems is not null)
			{
				collapsedItems.InsertRange(Math.Min(index, collapsedItems.Count), items);
				return;
			}
			base.InsertRange(index, items);
		}

		public override bool Remove(T? item)
		{
			if (collapsedItems is not null && collapsedItems.Remove(item!))
				return true;
			return base.Remove(item);
		}

		public override void RemoveAt(int index)
		{
			if (collapsedItems is not null)
			{
				if (index >= 0 && index < collapsedItems.Count)
					collapsedItems.RemoveAt(index);
				return;
			}
			base.RemoveAt(index);
		}

		public override void RemoveRange(int index, int count)
		{
			if (collapsedItems is not null)
			{
				if (index >= 0 && count > 0 && index + count <= collapsedItems.Count)
					collapsedItems.RemoveRange(index, count);
				return;
			}
			base.RemoveRange(index, count);
		}

		public override void ReplaceRange(int index, IEnumerable<T> items)
		{
			if (collapsedItems is not null)
			{
				var newItems = items.ToList();
				if (index >= 0 && index + newItems.Count <= collapsedItems.Count)
				{
					collapsedItems.RemoveRange(index, newItems.Count);
					collapsedItems.InsertRange(index, newItems);
				}
				return;
			}
			base.ReplaceRange(index, items);
		}

		public override void Clear()
		{
			collapsedItems = null;
			Model.IsCollapsed = false;
			base.Clear();
		}

		public override void Sort()
		{
			if (collapsedItems is not null)
			{
				collapsedItems.Sort();
				return;
			}
			base.Sort();
		}

		public override void Sort(Comparison<T> comparison)
		{
			if (collapsedItems is not null)
			{
				collapsedItems.Sort(comparison);
				return;
			}
			base.Sort(comparison);
		}

		public override void Order(Func<List<T>, IEnumerable<T>> func)
		{
			if (collapsedItems is not null)
			{
				collapsedItems = func.Invoke(collapsedItems).ToList();
				return;
			}
			base.Order(func);
		}

		public override void OrderOne(Func<List<T>, IEnumerable<T>> func, T item)
		{
			if (collapsedItems is not null)
			{
				var result = func.Invoke(collapsedItems).ToList();
				collapsedItems.Remove(item);
				var index = result.IndexOf(item);
				if (index != -1)
					collapsedItems.Insert(index, item);
				return;
			}
			base.OrderOne(func, item);
		}

		public void InitializeExtendedGroupHeaderInfoAsync()
		{
			if (GetExtendedGroupHeaderInfo is null)
				return;

			Model.ResumePropertyChangedNotifications(false);

			GetExtendedGroupHeaderInfo.Invoke(this);
			Model.Initialized = true;

			if (isBulkOperationStarted)
				Model.PausePropertyChangedNotifications();
		}

		public override void BeginBulkOperation()
		{
			base.BeginBulkOperation();

			Model.PausePropertyChangedNotifications();
		}

		public override void EndBulkOperation()
		{
			base.EndBulkOperation();

			Model.ResumePropertyChangedNotifications();
		}
	}
}
