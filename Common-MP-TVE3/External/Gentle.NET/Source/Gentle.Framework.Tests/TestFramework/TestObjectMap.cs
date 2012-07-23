using System.Reflection;
using Gentle.Common;
using NUnit.Framework;

/*
 * Test cases
 * Copyright (C) 2004 Clayton Harbour
 * 
 * This library is free software; you can redistribute it and/or modify it 
 * under the terms of the GNU Lesser General Public License 2.1 or later, as
 * published by the Free Software Foundation. See the included License.txt
 * or http://www.gnu.org/copyleft/lesser.html for details.
 *
 * $Id: TestObjectMap.cs 1232 2008-03-14 05:36:00Z mm $
 */

namespace Gentle.Framework.Tests
{
	/// <summary>
	/// Summary description for ObjectMapTest.
	/// </summary>
	[TestFixture]
	public class TestObjectMap
	{
		private PersistenceBroker broker;

		[SetUp]
		public void Init()
		{
			broker = new PersistenceBroker();
			Check.VerifyNotNull( broker.Provider, Error.DeveloperError,
			                     "No default provider information specified in the configuration file." );
		}

		/// <summary>
		/// Test case to verify the correct parsing of attributes and construction of the
		/// internally used ObjectMap instances.
		/// </summary>
		[Test]
		public void TestMapConstruction()
		{
			ObjectMap map = ObjectFactory.GetMap( null, typeof(PropertyHolder) );
			// test the primary key (autogenerated int)
			FieldMap fm = map.GetFieldMap( "Id" );
			Assert.IsTrue( ! fm.IsNullable );
			Assert.IsTrue( fm.IsValueType );
			Assert.IsTrue( ! fm.IsNullAssignable );
			Assert.IsTrue( fm.IsPrimaryKey );

			// AutoGenerated fields are not supported by Oracle, skip test.
			if( ! Broker.ProviderName.ToLower().StartsWith( "oracle" ) )
			{
				Assert.IsTrue( fm.IsAutoGenerated );
				Assert.IsTrue( fm.SequenceName.ToLower().Equals( "propertyholder_seq" ) );
			}

			// test a nullable int field  
			fm = map.GetFieldMap( "TInt" );
			Assert.IsTrue( fm.IsNullable );
			Assert.IsTrue( fm.IsValueType );
			Assert.IsTrue( ! fm.IsNullAssignable );
			Assert.IsNotNull( fm.NullValue );
			// test a nullable DateTime field  
			fm = map.GetFieldMap( "TDateTime" );
			Assert.IsTrue( fm.IsNullable );
			Assert.IsTrue( fm.IsValueType );
			Assert.IsTrue( ! fm.IsNullAssignable );
			Assert.IsNotNull( fm.NullValue );
		}

		/// <summary>
		/// Test case for verifying that column information (type, field size, constraints) 
		/// read from the database overrides the values specified using attributes.
		/// </summary>
		[Test]
		public void TestAnalyzerDataOverride()
		{
			PersistenceBroker broker = new PersistenceBroker();
			// Dont run test if the current provider does not have an Analyzer
			GentleAnalyzer analyzer = broker.Provider.GetAnalyzer();
			if( analyzer != null && GentleSettings.AnalyzerLevel != AnalyzerLevel.None )
			{
				ObjectMap map = ObjectFactory.GetMap( broker, typeof(PropertyHolder) );
				// test that analyzer data overrides any TableColumn attributes
				FieldMap fm = map.GetFieldMap( "TDouble" );
				if( analyzer.HasCapability( ColumnInformation.IsNullable ) )
				{
					Assert.IsTrue( fm.IsNullable );
				}
			}
		}

		[Test]
		public void TestObjectMapTraversal()
		{
			// make sure all types are known (ObjectMap has been created) before we start the test
			ObjectFactory.RegisterAssembly( null, Assembly.GetExecutingAssembly() );
			// perform the test
			ObjectMap sourceMap = ObjectFactory.GetMap( null, typeof(MemberPicture) );
			Assert.IsNotNull( sourceMap );
			FieldMap sourceField = sourceMap.GetFieldMap( "MemberId" );
			Assert.IsNotNull( sourceField );
			ObjectMap targetMap = ObjectFactory.GetMap( null, sourceField.ForeignKeyTableName );
			Assert.IsNotNull( targetMap );
			FieldMap targetField = targetMap.GetFieldMapFromColumn( sourceField.ForeignKeyColumnName );
			Assert.IsNotNull( targetField );
			Assert.AreEqual( "ListMember.MemberId", targetField.TableColumnName );
		}
	}
}