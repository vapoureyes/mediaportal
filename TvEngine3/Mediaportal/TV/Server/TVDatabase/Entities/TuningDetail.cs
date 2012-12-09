//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Mediaportal.TV.Server.TVDatabase.Entities
{
    [DataContract(IsReference = true)]
    [KnownType(typeof(Channel))]
    [KnownType(typeof(LnbType))]
    public partial class TuningDetail: IObjectWithChangeTracker, INotifyPropertyChanged
    {
        #region Primitive Properties
    
        [DataMember]
        public int IdTuning
        {
            get { return _idTuning; }
            set
            {
                if (_idTuning != value)
                {
                    if (ChangeTracker.ChangeTrackingEnabled && ChangeTracker.State != ObjectState.Added)
                    {
                        throw new InvalidOperationException("The property 'IdTuning' is part of the object's key and cannot be changed. Changes to key properties can only be made when the object is not being tracked or is in the Added state.");
                    }
                    _idTuning = value;
                    OnPropertyChanged("IdTuning");
                }
            }
        }
        private int _idTuning;
    
        [DataMember]
        public int IdChannel
        {
            get { return _idChannel; }
            set
            {
                if (_idChannel != value)
                {
                    ChangeTracker.RecordOriginalValue("IdChannel", _idChannel);
                    if (!IsDeserializing)
                    {
                        if (Channel != null && Channel.IdChannel != value)
                        {
                            Channel = null;
                        }
                    }
                    _idChannel = value;
                    OnPropertyChanged("IdChannel");
                }
            }
        }
        private int _idChannel;
    
        [DataMember]
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }
        private string _name;
    
        [DataMember]
        public string Provider
        {
            get { return _provider; }
            set
            {
                if (_provider != value)
                {
                    _provider = value;
                    OnPropertyChanged("Provider");
                }
            }
        }
        private string _provider;
    
        [DataMember]
        public int ChannelType
        {
            get { return _channelType; }
            set
            {
                if (_channelType != value)
                {
                    _channelType = value;
                    OnPropertyChanged("ChannelType");
                }
            }
        }
        private int _channelType;
    
        [DataMember]
        public int ChannelNumber
        {
            get { return _channelNumber; }
            set
            {
                if (_channelNumber != value)
                {
                    _channelNumber = value;
                    OnPropertyChanged("ChannelNumber");
                }
            }
        }
        private int _channelNumber;
    
        [DataMember]
        public int Frequency
        {
            get { return _frequency; }
            set
            {
                if (_frequency != value)
                {
                    _frequency = value;
                    OnPropertyChanged("Frequency");
                }
            }
        }
        private int _frequency;
    
        [DataMember]
        public int CountryId
        {
            get { return _countryId; }
            set
            {
                if (_countryId != value)
                {
                    _countryId = value;
                    OnPropertyChanged("CountryId");
                }
            }
        }
        private int _countryId;
    
        [DataMember]
        public int NetworkId
        {
            get { return _networkId; }
            set
            {
                if (_networkId != value)
                {
                    _networkId = value;
                    OnPropertyChanged("NetworkId");
                }
            }
        }
        private int _networkId;
    
        [DataMember]
        public int TransportId
        {
            get { return _transportId; }
            set
            {
                if (_transportId != value)
                {
                    _transportId = value;
                    OnPropertyChanged("TransportId");
                }
            }
        }
        private int _transportId;
    
        [DataMember]
        public int ServiceId
        {
            get { return _serviceId; }
            set
            {
                if (_serviceId != value)
                {
                    _serviceId = value;
                    OnPropertyChanged("ServiceId");
                }
            }
        }
        private int _serviceId;
    
        [DataMember]
        public int PmtPid
        {
            get { return _pmtPid; }
            set
            {
                if (_pmtPid != value)
                {
                    _pmtPid = value;
                    OnPropertyChanged("PmtPid");
                }
            }
        }
        private int _pmtPid;
    
        [DataMember]
        public bool FreeToAir
        {
            get { return _freeToAir; }
            set
            {
                if (_freeToAir != value)
                {
                    _freeToAir = value;
                    OnPropertyChanged("FreeToAir");
                }
            }
        }
        private bool _freeToAir;
    
        [DataMember]
        public int Modulation
        {
            get { return _modulation; }
            set
            {
                if (_modulation != value)
                {
                    _modulation = value;
                    OnPropertyChanged("Modulation");
                }
            }
        }
        private int _modulation;
    
        [DataMember]
        public int Polarisation
        {
            get { return _polarisation; }
            set
            {
                if (_polarisation != value)
                {
                    _polarisation = value;
                    OnPropertyChanged("Polarisation");
                }
            }
        }
        private int _polarisation;
    
        [DataMember]
        public int Symbolrate
        {
            get { return _symbolrate; }
            set
            {
                if (_symbolrate != value)
                {
                    _symbolrate = value;
                    OnPropertyChanged("Symbolrate");
                }
            }
        }
        private int _symbolrate;
    
        [DataMember]
        public int DiSEqC
        {
            get { return _diSEqC; }
            set
            {
                if (_diSEqC != value)
                {
                    _diSEqC = value;
                    OnPropertyChanged("DiSEqC");
                }
            }
        }
        private int _diSEqC;
    
        [DataMember]
        public int Bandwidth
        {
            get { return _bandwidth; }
            set
            {
                if (_bandwidth != value)
                {
                    _bandwidth = value;
                    OnPropertyChanged("Bandwidth");
                }
            }
        }
        private int _bandwidth;
    
        [DataMember]
        public int MajorChannel
        {
            get { return _majorChannel; }
            set
            {
                if (_majorChannel != value)
                {
                    _majorChannel = value;
                    OnPropertyChanged("MajorChannel");
                }
            }
        }
        private int _majorChannel;
    
        [DataMember]
        public int MinorChannel
        {
            get { return _minorChannel; }
            set
            {
                if (_minorChannel != value)
                {
                    _minorChannel = value;
                    OnPropertyChanged("MinorChannel");
                }
            }
        }
        private int _minorChannel;
    
        [DataMember]
        public int VideoSource
        {
            get { return _videoSource; }
            set
            {
                if (_videoSource != value)
                {
                    _videoSource = value;
                    OnPropertyChanged("VideoSource");
                }
            }
        }
        private int _videoSource;
    
        [DataMember]
        public int TuningSource
        {
            get { return _tuningSource; }
            set
            {
                if (_tuningSource != value)
                {
                    _tuningSource = value;
                    OnPropertyChanged("TuningSource");
                }
            }
        }
        private int _tuningSource;
    
        [DataMember]
        public int Band
        {
            get { return _band; }
            set
            {
                if (_band != value)
                {
                    _band = value;
                    OnPropertyChanged("Band");
                }
            }
        }
        private int _band;
    
        [DataMember]
        public int SatIndex
        {
            get { return _satIndex; }
            set
            {
                if (_satIndex != value)
                {
                    _satIndex = value;
                    OnPropertyChanged("SatIndex");
                }
            }
        }
        private int _satIndex;
    
        [DataMember]
        public int InnerFecRate
        {
            get { return _innerFecRate; }
            set
            {
                if (_innerFecRate != value)
                {
                    _innerFecRate = value;
                    OnPropertyChanged("InnerFecRate");
                }
            }
        }
        private int _innerFecRate;
    
        [DataMember]
        public int Pilot
        {
            get { return _pilot; }
            set
            {
                if (_pilot != value)
                {
                    _pilot = value;
                    OnPropertyChanged("Pilot");
                }
            }
        }
        private int _pilot;
    
        [DataMember]
        public int RollOff
        {
            get { return _rollOff; }
            set
            {
                if (_rollOff != value)
                {
                    _rollOff = value;
                    OnPropertyChanged("RollOff");
                }
            }
        }
        private int _rollOff;
    
        [DataMember]
        public string Url
        {
            get { return _url; }
            set
            {
                if (_url != value)
                {
                    _url = value;
                    OnPropertyChanged("Url");
                }
            }
        }
        private string _url;
    
        [DataMember]
        public int Bitrate
        {
            get { return _bitrate; }
            set
            {
                if (_bitrate != value)
                {
                    _bitrate = value;
                    OnPropertyChanged("Bitrate");
                }
            }
        }
        private int _bitrate;
    
        [DataMember]
        public int AudioSource
        {
            get { return _audioSource; }
            set
            {
                if (_audioSource != value)
                {
                    _audioSource = value;
                    OnPropertyChanged("AudioSource");
                }
            }
        }
        private int _audioSource;
    
        [DataMember]
        public bool IsVCRSignal
        {
            get { return _isVCRSignal; }
            set
            {
                if (_isVCRSignal != value)
                {
                    _isVCRSignal = value;
                    OnPropertyChanged("IsVCRSignal");
                }
            }
        }
        private bool _isVCRSignal;
    
        [DataMember]
        public int MediaType
        {
            get { return _mediaType; }
            set
            {
                if (_mediaType != value)
                {
                    _mediaType = value;
                    OnPropertyChanged("MediaType");
                }
            }
        }
        private int _mediaType;
    
        [DataMember]
        public int IdLnbType
        {
            get { return _idLnbType; }
            set
            {
                if (_idLnbType != value)
                {
                    ChangeTracker.RecordOriginalValue("IdLnbType", _idLnbType);
                    if (!IsDeserializing)
                    {
                        if (LnbType != null && LnbType.IdLnbType != value)
                        {
                            LnbType = null;
                        }
                    }
                    _idLnbType = value;
                    OnPropertyChanged("IdLnbType");
                }
            }
        }
        private int _idLnbType;

        #endregion
        #region Navigation Properties
    
        [DataMember]
        public Channel Channel
        {
            get { return _channel; }
            set
            {
                if (!ReferenceEquals(_channel, value))
                {
                    var previousValue = _channel;
                    _channel = value;
                    FixupChannel(previousValue);
                    OnNavigationPropertyChanged("Channel");
                }
            }
        }
        private Channel _channel;
    
        [DataMember]
        public LnbType LnbType
        {
            get { return _lnbType; }
            set
            {
                if (!ReferenceEquals(_lnbType, value))
                {
                    var previousValue = _lnbType;
                    _lnbType = value;
                    FixupLnbType(previousValue);
                    OnNavigationPropertyChanged("LnbType");
                }
            }
        }
        private LnbType _lnbType;

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
    
        // This entity type is the dependent end in at least one association that performs cascade deletes.
        // This event handler will process notifications that occur when the principal end is deleted.
        internal void HandleCascadeDelete(object sender, ObjectStateChangingEventArgs e)
        {
            if (e.NewState == ObjectState.Deleted)
            {
                this.MarkAsDeleted();
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
            Channel = null;
            LnbType = null;
        }

        #endregion
        #region Association Fixup
    
        private void FixupChannel(Channel previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.TuningDetails.Contains(this))
            {
                previousValue.TuningDetails.Remove(this);
            }
    
            if (Channel != null)
            {
                if (!Channel.TuningDetails.Contains(this))
                {
                    Channel.TuningDetails.Add(this);
                }
    
                IdChannel = Channel.IdChannel;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("Channel")
                    && (ChangeTracker.OriginalValues["Channel"] == Channel))
                {
                    ChangeTracker.OriginalValues.Remove("Channel");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("Channel", previousValue);
                }
                if (Channel != null && !Channel.ChangeTracker.ChangeTrackingEnabled)
                {
                    Channel.StartTracking();
                }
            }
        }
    
        private void FixupLnbType(LnbType previousValue)
        {
            if (IsDeserializing)
            {
                return;
            }
    
            if (previousValue != null && previousValue.TuningDetails.Contains(this))
            {
                previousValue.TuningDetails.Remove(this);
            }
    
            if (LnbType != null)
            {
                if (!LnbType.TuningDetails.Contains(this))
                {
                    LnbType.TuningDetails.Add(this);
                }
    
                IdLnbType = LnbType.IdLnbType;
            }
            if (ChangeTracker.ChangeTrackingEnabled)
            {
                if (ChangeTracker.OriginalValues.ContainsKey("LnbType")
                    && (ChangeTracker.OriginalValues["LnbType"] == LnbType))
                {
                    ChangeTracker.OriginalValues.Remove("LnbType");
                }
                else
                {
                    ChangeTracker.RecordOriginalValue("LnbType", previousValue);
                }
                if (LnbType != null && !LnbType.ChangeTracker.ChangeTrackingEnabled)
                {
                    LnbType.StartTracking();
                }
            }
        }

        #endregion
    }
}