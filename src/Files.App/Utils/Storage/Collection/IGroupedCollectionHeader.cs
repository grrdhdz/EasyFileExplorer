// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.App.Utils.Storage
{
	/// <summary>
	/// Represents an interface that is used to allow using x:Bind for the group header template.
	/// <br/>
	/// This is needed because x:Bind does not work with generic types, however it does work with interfaces.
	/// that are implemented by generic types.
	/// </summary>
	public interface IGroupedCollectionHeader
	{
		public GroupedHeaderViewModel Model { get; set; }

		/// <summary>
		/// Hides the group's items so only the header is rendered.
		/// </summary>
		public void Collapse();

		/// <summary>
		/// Restores the group's items after a collapse.
		/// </summary>
		public void Expand();
	}
}
