/*
 * The core attributes for decorating business objects
 * Copyright (C) 2004 Morten Mertner
 * 
 * This library is free software; you can redistribute it and/or modify it 
 * under the terms of the GNU Lesser General Public License 2.1 or later, as
 * published by the Free Software Foundation. See the included License.txt
 * or http://www.gnu.org/copyleft/lesser.html for details.
 *
 * $Id: PrimaryKeyAttribute.cs 1232 2008-03-14 05:36:00Z mm $
 */

using System;

namespace Gentle.Framework
{
	/// <summary>
	/// Use this attribute to designate properties that are primary key columns. This
	/// attribute must be present in addition to the <see cref="TableColumnAttribute"/> 
	/// attribute on all key properties.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true )]
	public sealed class PrimaryKeyAttribute : Attribute
	{
		private bool autoGenerated;

		/// <summary>
		/// Set this property to true for primary keys that are automatically assigned
		/// by the database on insert (identity columns in SQL server terminology). 
		/// </summary>
		public bool AutoGenerated
		{
			get { return autoGenerated; }
			set { autoGenerated = value; }
		}
	}
}