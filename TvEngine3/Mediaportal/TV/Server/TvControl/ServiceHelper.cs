﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Xml;

namespace Mediaportal.TV.Server.TVControl
{
  public static class ServiceHelper
  {
    public const int PortHttpService = 8000;
    public const int PortTcpService = 8001;
    private const int ReaderQuotasMaxDepth = Int32.MaxValue;
    private const int ReaderQuotasMaxStringContentLength = Int32.MaxValue;
    private const int ReaderQuotasMaxArrayLength = Int32.MaxValue;
    private const int ReaderQuotasMaxBytesPerRead = Int32.MaxValue;
    private const int ReaderQuotasMaxNameTableCharCount = Int32.MaxValue;
    private const int MaxBufferSize = Int32.MaxValue;
    private const int MaxReceivedMessageSize = Int32.MaxValue;
    private const string Tvservice = "/TVService/";
    private const BasicHttpSecurityMode HttpSecurityMode = BasicHttpSecurityMode.None;
    private const string NetTcpBindingName = "netTcpBinding";
    private const string DefaultBasicHttpBindingName = "defaultBasicHttpBinding";

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static NetTcpBinding GetTcpBinding()
    {      
      var netTcpBinding = new NetTcpBinding
      {
        Name = NetTcpBindingName,
        MaxBufferSize = MaxBufferSize,
        MaxReceivedMessageSize = MaxReceivedMessageSize,
      };
      SetReaderQuotas(netTcpBinding.ReaderQuotas);
      return netTcpBinding;
    }

    public static BasicHttpBinding GetHttpBinding()
    {
      var basicHttpBinding = new BasicHttpBinding
      {
        Name = DefaultBasicHttpBindingName,
        MaxBufferSize = MaxBufferSize,
        MaxReceivedMessageSize = MaxReceivedMessageSize,                               
        Security =
          {
            Mode = HttpSecurityMode,
            Transport = { ClientCredentialType = HttpClientCredentialType.None }
          }
      };
      SetReaderQuotas(basicHttpBinding.ReaderQuotas);
      return basicHttpBinding;
    }

    private static void SetReaderQuotas (XmlDictionaryReaderQuotas readerQuotas)
    {
      readerQuotas.MaxDepth = ReaderQuotasMaxDepth;
      readerQuotas.MaxStringContentLength = ReaderQuotasMaxStringContentLength;
      readerQuotas.MaxArrayLength = ReaderQuotasMaxArrayLength;
      readerQuotas.MaxBytesPerRead = ReaderQuotasMaxBytesPerRead;
      readerQuotas.MaxNameTableCharCount = ReaderQuotasMaxNameTableCharCount;
    }


    public static string GetEndpointURL(Type type, string hostName)
    {
      return @"http://" + hostName + ":" + PortHttpService + Tvservice + type.Name;
    }

    public static string GetTcpEndpointURL(Type type, string hostName)
    {
      return @"net.tcp://" + hostName + ":" + PortTcpService + Tvservice + type.Name;
    }
  }
}