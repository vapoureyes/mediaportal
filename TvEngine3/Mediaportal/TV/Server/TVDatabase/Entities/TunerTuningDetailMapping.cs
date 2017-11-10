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
    [KnownType(typeof(Tuner))]
    [KnownType(typeof(TuningDetail))]
    public partial class TunerTuningDetailMapping: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int IdTunerTuningDetailMapping
        {
            get { return _idTunerTuningDetailMapping; }
            set
            {
                if (_idTunerTuningDetailMapping != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'IdTunerTuningDetailMapping' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    _idTunerTuningDetailMapping = value;
                    OnPropertyChanged("IdTunerTuningDetailMapping");
                }
            }
        }
        private int _idTunerTuningDetailMapping;
    
        [DataMember]
        public int IdTuningDetail
        {
            get { return _idTuningDetail; }
            set
            {
                if (_idTuningDetail != value)
                {
                    ChangeTracker.RecordOriginalValue("IdTuningDetail", _idTuningDetail);
                    if (!IsDeserializing)
                    {
                        if (TuningDetail != null && TuningDetail.IdTuningDetail != value)
                        {
                            TuningDetail = null;
                        }
                    }
                    _idTuningDetail = value;
                    OnPropertyChanged("IdTuningDetail");
                }
            }
        }
        private int _idTuningDetail;
    
        [DataMember]
        public int IdTuner
        {
            get { return _idTuner; }
            set
            {
                if (_idTuner != value)
                {
                    ChangeTracker.RecordOriginalValue("IdTuner", _idTuner);
                    if (!IsDeserializing)
                    {
                        if (Tuner != null && Tuner.IdTuner != value)
                        {
                            Tuner = null;
                        }
                    }
                    _idTuner = value;
                    OnPropertyChanged("IdTuner");
                }
            }
        }
        private int _idTuner;

        #endregion
        #region Navigation Properties
    
        [DataMember]
        public Tuner Tuner
        {
            get { return _tuner; }
            set
            {
                if (!ReferenceEquals(_tuner, value))
                {
                    var previousValue = _tuner;
                    _tuner = value;
                    FixupTuner(previousValue);
                    OnNavigationPropertyChanged("Tuner");
                }
            }
        }
        private Tuner _tuner;
    
        [DataMember]
        public TuningDetail TuningDetail
        {
            get { return _tuningDetail; }
            set
            {
                if (!ReferenceEquals(_tuningDetail, value))
                {
                    var previousValue = _tuningDetail;
                    _tuningDetail = value;
                    FixupTuningDetail(previousValue);
                    OnNavigationPropertyChanged("TuningDetail");
                }
            }
        }
        private TuningDetail _tuningDetail;

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
            Tuner = null;
            TuningDetail = null;
        }

        #endregion
        #region Association Fixup
    
        private void FixupTuner(Tuner previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.TuningDetailMappings.Contains(this))
            {
                previousValue.TuningDetailMappings.Remove(this);
            }
    
            if (Tuner != null)
            {
                if (!Tuner.TuningDetailMappings.Contains(this))
                {
                    Tuner.TuningDetailMappings.Add(this);
                }
    
                IdTuner = Tuner.IdTuner;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("Tuner")
                    && (ChangeTracker.OriginalValues["Tuner"] == Tuner))
                {
                    ChangeTracker.OriginalValues.Remove("Tuner");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("Tuner", previousValue);
                }
                if (Tuner != null && !Tuner.ChangeTracker.ChangeTrackingEnabled)
                {
                    Tuner.StartTracking();
                }
            }
        }
    
        private void FixupTuningDetail(TuningDetail previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.TunerMappings.Contains(this))
            {
                previousValue.TunerMappings.Remove(this);
            }
    
            if (TuningDetail != null)
            {
                if (!TuningDetail.TunerMappings.Contains(this))
                {
                    TuningDetail.TunerMappings.Add(this);
                }
    
                IdTuningDetail = TuningDetail.IdTuningDetail;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("TuningDetail")
                    && (ChangeTracker.OriginalValues["TuningDetail"] == TuningDetail))
                {
                    ChangeTracker.OriginalValues.Remove("TuningDetail");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("TuningDetail", previousValue);
                }
                if (TuningDetail != null && !TuningDetail.ChangeTracker.ChangeTrackingEnabled)
                {
                    TuningDetail.StartTracking();
                }
            }
        }

        #endregion
    }
}