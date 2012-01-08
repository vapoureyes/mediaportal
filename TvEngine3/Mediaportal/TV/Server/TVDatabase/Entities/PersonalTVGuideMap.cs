//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace Mediaportal.TV.Server.TVDatabase.Entities
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(Program))]
    [KnownType(typeof(Keyword))]
    public partial class PersonalTVGuideMap: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int idPersonalTVGuideMap
        {
            get { return _idPersonalTVGuideMap; }
            set
            {
                if (_idPersonalTVGuideMap != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'idPersonalTVGuideMap' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    _idPersonalTVGuideMap = value;
                    OnPropertyChanged("idPersonalTVGuideMap");
                }
            }
        }
        private int _idPersonalTVGuideMap;
    
        [DataMember]
        public int idKeyword
        {
            get { return _idKeyword; }
            set
            {
                if (_idKeyword != value)
                {
                    ChangeTracker.RecordOriginalValue("idKeyword", _idKeyword);
                    if (!IsDeserializing)
                    {
                        if (Keyword != null && Keyword.idKeyword != value)
                        {
                            Keyword = null;
                        }
                    }
                    _idKeyword = value;
                    OnPropertyChanged("idKeyword");
                }
            }
        }
        private int _idKeyword;
    
        [DataMember]
        public int idProgram
        {
            get { return _idProgram; }
            set
            {
                if (_idProgram != value)
                {
                    ChangeTracker.RecordOriginalValue("idProgram", _idProgram);
                    if (!IsDeserializing)
                    {
                        if (Program != null && Program.idProgram != value)
                        {
                            Program = null;
                        }
                    }
                    _idProgram = value;
                    OnPropertyChanged("idProgram");
                }
            }
        }
        private int _idProgram;

        #endregion
        #region Navigation Properties
    
        [DataMember]
        public Program Program
        {
            get { return _program; }
            set
            {
                if (!ReferenceEquals(_program, value))
                {
                    var previousValue = _program;
                    _program = value;
                    FixupProgram(previousValue);
                    OnNavigationPropertyChanged("Program");
                }
            }
        }
        private Program _program;
    
        [DataMember]
        public Keyword Keyword
        {
            get { return _keyword; }
            set
            {
                if (!ReferenceEquals(_keyword, value))
                {
                    var previousValue = _keyword;
                    _keyword = value;
                    FixupKeyword(previousValue);
                    OnNavigationPropertyChanged("Keyword");
                }
            }
        }
        private Keyword _keyword;

        #endregion
        #region ChangeTracking
    
        protected virtual void OnPropertyChanged(String propertyName)
        {
            if (ChangeTracker.State != ObjectState.Added && ChangeTracker.State != ObjectState.Deleted)
            {
                ChangeTracker.State = ObjectState.Modified;
            }
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        protected virtual void OnNavigationPropertyChanged(String propertyName)
        {
            if (_propertyChanged != null)
            {
                _propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged{ add { _propertyChanged += value; } remove { _propertyChanged -= value; } }
        private event PropertyChangedEventHandler _propertyChanged;
        private ObjectChangeTracker _changeTracker;
    
        [DataMember]
        public ObjectChangeTracker ChangeTracker
        {
            get
            {
                if (_changeTracker == null)
                {
                    _changeTracker = new ObjectChangeTracker();
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
                return _changeTracker;
            }
            set
            {
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging -= HandleObjectStateChanging;
                }
                _changeTracker = value;
                if(_changeTracker != null)
                {
                    _changeTracker.ObjectStateChanging += HandleObjectStateChanging;
                }
            }
        }
    
        private void HandleObjectStateChanging(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                ClearNavigationProperties();
            }
        }
    
        protected bool IsDeserializing { get; private set; }
    
        [OnDeserializing]
        public void OnDeserializingMethod(StreamingContext context)
        {
            IsDeserializing = true;
        }
    
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context)
        {
            IsDeserializing = false;
            ChangeTracker.ChangeTrackingEnabled = true;
        }
    
        // This entity type is the dependent end in at least one association that performs cascade deletes.
        // This event handler will process notifications that occur when the principal end is deleted.
        internal void HandleCascadeDelete(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                this.MarkAsDeleted();
            }
        }
    
        protected virtual void ClearNavigationProperties()
        {
            Program = null;
            Keyword = null;
        }

        #endregion
        #region Association Fixup
    
        private void FixupProgram(Program previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.PersonalTVGuideMaps.Contains(this))
            {
                previousValue.PersonalTVGuideMaps.Remove(this);
            }
    
            if (Program != null)
            {
                if (!Program.PersonalTVGuideMaps.Contains(this))
                {
                    Program.PersonalTVGuideMaps.Add(this);
                }
    
                idProgram = Program.idProgram;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("Program")
                    && (ChangeTracker.OriginalValues["Program"] == Program))
                {
                    ChangeTracker.OriginalValues.Remove("Program");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("Program", previousValue);
                }
                if (Program != null && !Program.ChangeTracker.ChangeTrackingEnabled)
                {
                    Program.StartTracking();
                }
            }
        }
    
        private void FixupKeyword(Keyword previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.PersonalTVGuideMaps.Contains(this))
            {
                previousValue.PersonalTVGuideMaps.Remove(this);
            }
    
            if (Keyword != null)
            {
                if (!Keyword.PersonalTVGuideMaps.Contains(this))
                {
                    Keyword.PersonalTVGuideMaps.Add(this);
                }
    
                idKeyword = Keyword.idKeyword;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("Keyword")
                    && (ChangeTracker.OriginalValues["Keyword"] == Keyword))
                {
                    ChangeTracker.OriginalValues.Remove("Keyword");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("Keyword", previousValue);
                }
                if (Keyword != null && !Keyword.ChangeTracker.ChangeTrackingEnabled)
                {
                    Keyword.StartTracking();
                }
            }
        }

        #endregion
    }
}