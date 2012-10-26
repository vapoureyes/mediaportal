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
    public partial class Timespan: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int IdTimespan
        {
            get { return _idTimespan; }
            set
            {
                if (_idTimespan != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'IdTimespan' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    _idTimespan = value;
                    OnPropertyChanged("IdTimespan");
                }
            }
        }
        private int _idTimespan;
    
        [DataMember]
        public System.DateTime StartTime
        {
            get { return _startTime; }
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged("StartTime");
                }
            }
        }
        private System.DateTime _startTime;
    
        [DataMember]
        public System.DateTime EndTime
        {
            get { return _endTime; }
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged("EndTime");
                }
            }
        }
        private System.DateTime _endTime;
    
        [DataMember]
        public int DayOfWeek
        {
            get { return _dayOfWeek; }
            set
            {
                if (_dayOfWeek != value)
                {
                    _dayOfWeek = value;
                    OnPropertyChanged("DayOfWeek");
                }
            }
        }
        private int _dayOfWeek;
    
        [DataMember]
        public int IdKeyword
        {
            get { return _idKeyword; }
            set
            {
                if (_idKeyword != value)
                {
                    _idKeyword = value;
                    OnPropertyChanged("IdKeyword");
                }
            }
        }
        private int _idKeyword;

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
    
        protected virtual void ClearNavigationProperties()
        {
        }

        #endregion
    }
}