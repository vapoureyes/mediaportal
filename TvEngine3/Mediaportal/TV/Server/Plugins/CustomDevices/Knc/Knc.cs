﻿#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.Knc
{
  /// <summary>
  /// A class for handling conditional access and DiSEqC for KNC One tuners, including compatible
  /// models from Mystique and Satelco.
  /// </summary>
  public class Knc : BaseCustomDevice, IConditionalAccessProvider, IConditionalAccessMenuActions, IDiseqcDevice
  {
    #region enums

    private enum KncCiState
    {
      Initialising = 0,       // Indicates that a CAM has been inserted.

      Transport = 1,
      Resource = 2,
      Application = 3,
      ConditionalAccess = 4,

      Ready = 5,              // Indicates that the CAM is ready for interaction.
      OpenService = 6,
      Releasing = 7,          // Indicates that the CAM has been removed.

      CloseMmi = 8,
      Request = 9,
      Menu = 10,
      MenuChoice = 11,

      OpenDisplay = 12,
      CloseDisplay = 13,

      None = 99
    }

    #endregion

    #region DLL imports

    /// <summary>
    /// Enable the conditional access interface.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="graphBuilder">The graph containing the tuner filter. Note: should be set to null for non-PCIe tuners.</param>
    /// <param name="filter">The filter which supports the proprietary property sets. This is the tuner filter for PCI tuners and the capture filter for PCI-e tuners.</param>
    /// <param name="callBacks">Call back structure pointer.</param>
    /// <returns><c>true</c> if the interface is successfully enabled, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_Enable(int deviceIndex, IGraphBuilder graphBuilder, IBaseFilter filter, IntPtr callBacks);

    /// <summary>
    /// Disable the conditional access interface.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <returns><c>true</c> if the interface is successfully disabled, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_Disable(int deviceIndex);

    /// <summary>
    /// Check if this tuner currently has conditional access capabilities.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <returns><c>true</c> if a CI slot is connected with a CAM inserted, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_IsAvailable(int deviceIndex);

    /// <summary>
    /// Check if the CAM is ready for interaction.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <returns><c>true</c> if the CAM is ready, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_IsReady(int deviceIndex);

    /// <summary>
    /// Enable the use of the KNCBDA_CI_* functions by initialising internal variables and interfaces.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="param"><c>True</c> to enable the CI slot; <c>false</c>to disable the CI slot.</param>
    /// <returns>???</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_HW_Enable(int deviceIndex, [MarshalAs(UnmanagedType.Bool)] bool param);

    /// <summary>
    /// Get the name/brand/type of the CAM inserted in the CI slot. This string is likely
    /// to be the root menu entry from the CA information.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="name">A buffer to hold the CAM name.</param>
    /// <param name="bufferSize">The size of the CAM name buffer in bytes.</param>
    /// <returns>the name/brand/type of the CAM</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_GetName(int deviceIndex, [MarshalAs(UnmanagedType.LPStr)] StringBuilder name,
                                                uint bufferSize);

    /// <summary>
    /// Send CA PMT to the CAM.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="caPmt">A pointer to a buffer containing the CA PMT.</param>
    /// <param name="caPmtLength">The length of the CA PMT buffer in bytes.</param>
    /// <returns><c>true</c> if the CA PMT is successfully passed to the CAM, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_SendPMTCommand(int deviceIndex, IntPtr caPmt, uint caPmtLength);

    /// <summary>
    /// Enter the CAM menu.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="slotIndex">The index (0..n) of the CI slot that the CAM is inserted in.</param>
    /// <returns>???</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_EnterMenu(int deviceIndex, byte slotIndex);

    /// <summary>
    /// Select an entry in the CAM menu.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="slotIndex">The index (0..n) of the CI slot that the CAM is inserted in.</param>
    /// <param name="choice">The index (0..n) of the menu choice selected by the user.</param>
    /// <returns>???</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_SelectMenu(int deviceIndex, byte slotIndex, byte choice);

    /// <summary>
    /// Close the CAM menu.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="slotIndex">The index (0..n) of the CI slot that the CAM is inserted in.</param>
    /// <returns>???</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_CloseMenu(int deviceIndex, byte slotIndex);

    /// <summary>
    /// Send an answer to an enquiry from the user to the CAM.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="slotIndex">The index (0..n) of the CI slot that the CAM is inserted in.</param>
    /// <param name="cancel"><c>True</c> to cancel the enquiry.</param>
    /// <param name="answer">The user's answer to the enquiry.</param>
    /// <returns>???</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_CI_SendMenuAnswer(int deviceIndex, byte slotIndex, [MarshalAs(UnmanagedType.Bool)] bool cancel,
                                                       [In, MarshalAs(UnmanagedType.LPStr)] string answer);

    /// <summary>
    /// Enable the use of the KNCBDA_HW_* functions by initialising internal variables and interfaces.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="filter">The filter which supports the proprietary property sets. This is the tuner filter for PCI tuners and the capture filter for PCI-e tuners.</param>
    /// <returns><c>true</c> if the hardware interfaces are successfully initialised, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_HW_Enable(int deviceIndex, IBaseFilter filter);

    /// <summary>
    /// Send a DiSEqC command.
    /// </summary>
    /// <param name="deviceIndex">Device index 0..n.</param>
    /// <param name="command">A pointer to a buffer containing the command.</param>
    /// <param name="commandLength">The length of the command.</param>
    /// <param name="repeatCount">The number of times to resend the command.</param>
    /// <returns><c>true</c> if the tuner successfully sent the command, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KNCBDA_HW_DiSEqCWrite(int deviceIndex, IntPtr command, uint commandLength, uint repeatCount);

    /// <summary>
    /// PCI-e products (Philips/NXP/Trident SAA7160 based) have a main device filter - one
    /// main device filter per card. This function enumerates and returns the total number of
    /// main devices filters in the system. Note that this function does *not* exclude non-KNC
    /// SAA716x based main devices.
    /// </summary>
    /// <returns>the number of SAA716x main device filters in the system</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int PCIE_EnumerateMainDevices();

    /// <summary>
    /// Returns the filter name for a specific main device.
    /// </summary>
    /// <param name="mainDeviceIndex">Main device index 0..n.</param>
    /// <returns>the name for the main device corresponding with the index parameter</returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.LPStr)]
    private static extern string PCIE_GetDeviceItem(int mainDeviceIndex);

    /// <summary>
    /// Open a main device. You need to do this if you want to swap the CI/CAM inputs on the corresponding
    /// card. You can only have one main device open at a time.
    /// </summary>
    /// <param name="mainDeviceIndex">Main device index 0..n.</param>
    /// <returns><c>true</c> if the device is successfully opened, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PCIE_OpenMainDevice(int mainDeviceIndex);

    /// <summary>
    /// Close the currently open main device. You can only have one main device open at a time.
    /// </summary>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void PCIE_CloseMainDevice();

    /// <summary>
    /// Set the value of a tuner property.
    /// </summary>
    /// <param name="propertySet">The GUID of the property set.</param>
    /// <param name="propertyIndex">The index of the property within the property set.</param>
    /// <param name="data">A pointer to a buffer containing the property value.</param>
    /// <param name="dataLength">The length of the property value in bytes.</param>
    /// <returns><c>true</c> if the value of the property is set successfully, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PCIE_SetProperty(Guid propertySet, uint propertyIndex, IntPtr data, uint dataLength);

    /// <summary>
    /// Get the value of a tuner property.
    /// </summary>
    /// <param name="propertySet">The GUID of the property set.</param>
    /// <param name="propertyIndex">The index of the property within the property set.</param>
    /// <param name="data">A pointer to a buffer to hold the property value.</param>
    /// <param name="dataLength">The length of the property value in bytes.</param>
    /// <returns><c>true</c> if the value of the property is retrieved successfully, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PCIE_GetProperty(Guid propertySet, uint propertyIndex, IntPtr data, out uint dataLength);

    /// <summary>
    /// Swap the CI slot/CAM associated with the tuners on a card. Tuners can only access a single
    /// CI slot/CAM at any given time.
    /// </summary>
    /// <param name="swap"><c>True</c> to swap CI slot/CAM inputs.</param>
    /// <returns><c>true</c> if the CI slot/CAM inputs on the device are successfully swapped, otherwise <c>false</c></returns>
    [DllImport("Resources\\KNCBDACTRL.dll", CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PCIE_SwapCAMInput([MarshalAs(UnmanagedType.Bool)] bool swap);

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct KncCiCallBacks
    {
      /// Optional context that the interface will pass back
      /// as a parameter when the delegates are executed.
      public IntPtr Context;

      /// Delegate for CI state changes.
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiState OnCiState;

      /// Delegate for entering the CAM menu.
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiOpenDisplay OnOpenDisplay;

      /// Delegate for CAM menu meta-data (header, footer etc.).
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiMenu OnCiMenu;

      /// Delegate for CAM menu entries.
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiMenuEntry OnCiMenuEntry;

      /// Delegate for CAM requests.
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiRequest OnRequest;

      /// Delegate for closing the CAM menu.
      [MarshalAs(UnmanagedType.FunctionPtr)]
      public OnKncCiCloseDisplay OnCloseDisplay;
    }

    #endregion

    #region call back definitions

    /// <summary>
    /// Called by the tuner driver when the state of a CI slot changes.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot that changed state.</param>
    /// <param name="state">The new state of the slot.</param>
    /// <param name="menuTitle">The CAM root menu title. This will be blank when the CAM has not been completely
    ///   initialised. Typically this will be the same string as can be retrieved by KNCBDA_CI_GetName().</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiState(
      byte slotIndex, KncCiState state, [MarshalAs(UnmanagedType.LPStr)] string menuTitle, IntPtr context);

    /// <summary>
    /// Called by the tuner driver when the CAM menu is successfully opened.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiOpenDisplay(byte slotIndex, IntPtr context);

    /// <summary>
    /// Called by the tuner driver to pass the menu meta-data when the user is browsing the CAM menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="title">The menu title.</param>
    /// <param name="subTitle">The menu sub-title.</param>
    /// <param name="footer">The menu footer.</param>
    /// <param name="entryCount">The number of entries in the menu.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiMenu(byte slotIndex, [MarshalAs(UnmanagedType.LPStr)] string title,
                                            [MarshalAs(UnmanagedType.LPStr)] string subTitle,
                                            [MarshalAs(UnmanagedType.LPStr)] string footer,
                                            uint entryCount, IntPtr context);

    /// <summary>
    /// Called by the tuner driver for each menu entry when the user is browsing the CAM menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="entryIndex">The index of the entry within the menu.</param>
    /// <param name="text">The text associated with the entry.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiMenuEntry(
      byte slotIndex, uint entryIndex, [MarshalAs(UnmanagedType.LPStr)] string text, IntPtr context);

    /// <summary>
    /// Called by the tuner driver when the CAM requests input from the user.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="blind"><c>True</c> if the input should be hidden (eg. password).</param>
    /// <param name="answerLength">The expected answer length.</param>
    /// <param name="text">The request context text from the CAM.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiRequest(
      byte slotIndex, bool blind, uint answerLength, [MarshalAs(UnmanagedType.LPStr)] string text, IntPtr context);

    /// <summary>
    /// Called by the tuner driver when the CAM wants to close the menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="delay">The delay (in milliseconds) after which the menu should be closed.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnKncCiCloseDisplay(byte slotIndex, uint delay, IntPtr context);

    #endregion

    #region constants

    private static readonly string[] VALID_TUNER_NAMES = new string[]
    {
      "KNC BDA DVB-S",
      "KNC BDA DVB-S2",
      "KNC BDA DVB-C",
      "KNC BDA DVB-T",
      "7160 KNC BDA DVBS2 Tuner",   // PCI-e: DVB-S2 Twin

      "Mystique SaTiX DVB-S",
      "Mystique SaTiX DVB-S2",
      "Mystique CaBiX DVB-C2",
      "Mystique TeRiX DVB-T2",
      "Mystique SaTiX-S",
      "Mystique SaTiX-S2",
      "Mystique CaBiX-C2",
      "Mystique TeRiX-T2",

      "Satelco EasyWatch PCI (DVB-S)",
      "Satelco EasyWatch PCI (DVB-S2)",
      "Satelco EasyWatch PCI (DVB-C)",
      "Satelco EasyWatch PCI (DVB-T)"
    };

    private static readonly string[] VALID_DEVICE_PATHS = new string[]
    {
      // DVB-S - Old
      "ven_1131&dev_7146&subsys_4f561131",  // KNC

      // DVB-S - SH2
      "ven_1131&dev_7146&subsys_00101894",  // KNC
      "ven_1131&dev_7146&subsys_00111894",  // Mystique
      "ven_1131&dev_7146&subsys_001a1894",  // Satelco

      // DVB-S - X4
      "ven_1131&dev_7146&subsys_00161894",  // KNC
      "ven_1131&dev_7146&subsys_00151894",  // Mystique
      "ven_1131&dev_7146&subsys_001b1894",  // Satelco

      // DVB-S - X4 (no CI)
      "ven_1131&dev_7146&subsys_00141894",  // KNC
      "ven_1131&dev_7146&subsys_001e1894",  // Satelco

      // DVB-S - X6
      "ven_1131&dev_7146&subsys_00191894",  // KNC
      "ven_1131&dev_7146&subsys_00181894",  // Mystique
      "ven_1131&dev_7146&subsys_001d1894",  // Satelco
      "ven_1131&dev_7146&subsys_001f1894",  // Satelco

      // DVB-S2 - Sharp
      "ven_1131&dev_7146&subsys_00501894",  // KNC
      "ven_1131&dev_7146&subsys_00511894",  // Mystique
      "ven_1131&dev_7146&subsys_00521894",  // Satelco

      // DVB-S - X8
      "ven_1131&dev_7146&subsys_00561894",  // KNC
      "ven_1131&dev_7146&subsys_00551894",  // Mystique
      "ven_1131&dev_7146&subsys_005b1894",  // Satelco

      // DVB-S - X8 (no CI)
      "ven_1131&dev_7146&subsys_00541894",  // KNC
      "ven_1131&dev_7146&subsys_005e1894",  // Satelco

      // DVB-C - MK2
      "ven_1131&dev_7146&subsys_00201894",  // KNC
      "ven_1131&dev_7146&subsys_00211894",  // Mystique
      "ven_1131&dev_7146&subsys_002a1894",  // Satelco

      // DVB-C - MK3
      "ven_1131&dev_7146&subsys_00221894",  // KNC
      "ven_1131&dev_7146&subsys_00231894",  // Mystique
      "ven_1131&dev_7146&subsys_002c1894",  // Satelco

      // DVB-C - MK32
      "ven_1131&dev_7146&subsys_00281894",  // KNC

      // DVB-T
      "ven_1131&dev_7146&subsys_00301894",  // KNC
      "ven_1131&dev_7146&subsys_00311894",  // Mystique
      "ven_1131&dev_7146&subsys_003a1894",  // Satelco

      // New KNC PCI-e tuners
      "ven_1131&dev_7160&subsys_01101894",  // DVB-S/DVB-S2 (not yet released)
      "ven_1131&dev_7160&subsys_02101894",  // DVB-S2/DVB-S2 (DVB-S2 Twin)
      "ven_1131&dev_7160&subsys_03101894",  // DVB-T/DVB-C (not yet released)
    };

    private static readonly int CALLBACK_SET_SIZE = Marshal.SizeOf(typeof(KncCiCallBacks));   // 28
    // This limit is based on the Omicom SDK details. My understanding is that
    // KNCBDACTRL.dll uses the same API/SDK (for DiSEqC) internally.
    private const int MAX_DISEQC_COMMAND_LENGTH = 64;

    #endregion

    #region variables

    private bool _isKnc = false;
    private bool _isCaInterfaceOpen = false;
    private bool _isPcie = false;
    private int _deviceIndex = -1;
    private byte _slotIndex = 0;
    private string _name = "KNC";
    private KncCiState _ciState = KncCiState.Releasing;
    private bool _isCamPresent = false;
    private bool _isCamReady = false;

    private IntPtr _diseqcBuffer = IntPtr.Zero;
    private IntPtr _callBackBuffer = IntPtr.Zero;

    private IBaseFilter _tunerFilter = null;
    private IBaseFilter _captureFilter = null;
    private IGraphBuilder _graph = null;

    private KncCiCallBacks _ciCallBacks;
    private IConditionalAccessMenuCallBacks _caMenuCallBacks = null;
    private object _caMenuCallBackLock = new object();

    #endregion

    /// <summary>
    /// Calculates the correct device index for a given tuner. The index represents a position in an
    /// ordered list of KNC-compatible tuners installed in the system.
    /// </summary>
    /// <remarks>
    /// The device index may in fact be arbitrary as long as we ensure that it is unique, but I have not
    /// been able to determine this with certainty.
    /// </remarks>
    /// <param name="devicePath">The device path of the tuner device.</param>
    /// <returns>the device index for this tuner, otherwise -1 if the tuner is not KNC-compatible</returns>
    private static int GetDeviceIndex(string devicePath)
    {
      // Build a list of the device paths of all KNC-compatible tuners installed in this system.
      DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.BDASourceFiltersCategory);
      List<string> devicePaths = new List<string>();
      foreach (DsDevice device in devices)
      {
        foreach (string validTunerName in VALID_TUNER_NAMES)
        {
          if (device.Name != null && device.Name.Equals(validTunerName))
          {
            devicePaths.Add(device.DevicePath);
            break;
          }
        }
        device.Dispose();
      }

      if (devicePaths.Count == 0)
      {
        return -1;
      }

      // Sort the list - we want the device paths in a consistent order.
      devicePaths.Sort();

      // Find the index of the device path.
      int idx = 0;
      foreach (string path in devicePaths)
      {
        if (devicePath.Equals(path))
        {
          return idx;
        }
        idx++;
      }
      return -1;
    }

    /// <summary>
    /// Swap the CI-slot-to-tuner association. Normally the first CI slot is always associated with the first
    /// tuner; likewise, the second CI slot is associated with the second tuner. This function allows us to
    /// associate the first CI slot with the second tuner and the second CI slot with the first tuner.
    /// </summary>
    /// <remarks>
    /// This function is only applicable for PCIe tuners such as the KNC TV-Station DVB-S2 Twin. I can't
    /// think of any realistic scenarios where it would be advantageous to do this - in most cases where only
    /// one CI slot is connected, optimal behaviour can be achieved by mapping channels and prioritising
    /// tuners appropriately.
    /// </remarks>
    /// <returns><c>true</c> if the association is successfully swapped, otherwise <c>false</c></returns>
    private bool SwapCardCiSlots()
    {
      this.LogDebug("KNC: device {0} swap CI slots", _deviceIndex);

      if (!_isKnc)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isPcie)
      {
        this.LogDebug("KNC: function not supported");
        return false;
      }

      // Note: num716xDevices includes non-KNC SAA716x main devices. This has been proven with SAA7162 based
      // Pinnacle 7010ix cards.
      int num716xDevices = PCIE_EnumerateMainDevices();
      this.LogDebug("KNC: main device count is {0}", num716xDevices);
      if (num716xDevices == 0)
      {
        return false;
      }

      // We have a bit of a problem here: how do we know which main device this tuner is associated with?
      // The mainDeviceName is a filter name. If it were a device path then we might be able to correlate
      // it with this tuner's device path instance component. In addition, the mainDeviceName is non-unique,
      // so in the case where there are multiple KNC PCIe devices connected to a single system we are not
      // able to distinguish between them. Hmmm...
      string mainDeviceName = PCIE_GetDeviceItem(0);

      if (!PCIE_OpenMainDevice(0))
      {
        this.LogError("KNC: failed to open main device");
        return false;
      }

      bool success = PCIE_SwapCAMInput(true);
      PCIE_CloseMainDevice();
      this.LogDebug("KNC: result = {0}", success);
      return success;
    }

    #region call back handlers

    /// <summary>
    /// Called by the tuner driver when the state of a CI slot changes.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot that changed state.</param>
    /// <param name="state">The new state of the slot.</param>
    /// <param name="menuTitle">The CAM root menu title.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiState(byte slotIndex, KncCiState state, string menuTitle, IntPtr context)
    {
      this.LogInfo("KNC: device {0} CI state change call back, slot = {1}", _deviceIndex, slotIndex);
      if (state == KncCiState.Ready)
      {
        _isCamPresent = true;
        _isCamReady = true;
      }
      else if (state == KncCiState.Initialising)
      {
        _isCamPresent = true;
        _isCamReady = false;
      }
      else if (state == KncCiState.Releasing)
      {
        _isCamPresent = false;
        _isCamReady = false;
      }
      this.LogInfo("  old state  = {0}", _ciState);
      this.LogInfo("  new state  = {0}", state);
      this.LogDebug("  menu title = {0}", menuTitle);
      _ciState = state;
    }

    /// <summary>
    /// Called by the tuner driver when the CAM menu is successfully opened.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiOpenDisplay(byte slotIndex, IntPtr context)
    {
      this.LogInfo("KNC: device {0} open menu call back, slot = {1}", _deviceIndex, slotIndex);
    }

    /// <summary>
    /// Called by the tuner driver to pass the menu meta-data when the user is browsing the CAM menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="title">The menu title.</param>
    /// <param name="subTitle">The menu sub-title.</param>
    /// <param name="footer">The menu footer.</param>
    /// <param name="entryCount">The number of entries in the menu.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiMenu(byte slotIndex, string title, string subTitle, string footer, uint entryCount, IntPtr context)
    {
      this.LogInfo("KNC: device {0} menu call back, slot = {1}", _deviceIndex, slotIndex);
      this.LogDebug("  title     = {0}", title);
      this.LogDebug("  sub-title = {0}", subTitle);
      this.LogDebug("  footer    = {0}", footer);
      this.LogDebug("  # entries = {0}", entryCount);
      lock (_caMenuCallBackLock)
      {
        if (_caMenuCallBacks != null)
        {
          _caMenuCallBacks.OnCiMenu(title, subTitle, footer, (int)entryCount);
        }
        else
        {
          this.LogDebug("KNC: menu call backs are not set");
        }
      }
    }

    /// <summary>
    /// Called by the tuner driver for each menu entry when the user is browsing the CAM menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="entryIndex">The index of the entry within the menu.</param>
    /// <param name="text">The text associated with the entry.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiMenuEntry(byte slotIndex, uint entryIndex, string text, IntPtr context)
    {
      this.LogDebug("KNC: device {0} menu entry call back, slot = {1}", _deviceIndex, slotIndex);
      this.LogDebug("  entry {0, -2} = {1}", entryIndex, text);
      lock (_caMenuCallBackLock)
      {
        if (_caMenuCallBacks != null)
        {
          _caMenuCallBacks.OnCiMenuChoice((int)entryIndex, text);
        }
        else
        {
          this.LogDebug("KNC: menu call backs are not set");
        }
      }
    }

    /// <summary>
    /// Called by the tuner driver when the CAM requests input from the user.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="blind"><c>True</c> if the input should be hidden (eg. password).</param>
    /// <param name="answerLength">The expected answer length.</param>
    /// <param name="prompt">Contextual text from the CAM.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiRequest(byte slotIndex, bool blind, uint answerLength, string prompt, IntPtr context)
    {
      this.LogInfo("KNC: device {0} request call back, slot = {1}", _deviceIndex, slotIndex);
      this.LogDebug("  prompt = {0}", prompt);
      this.LogDebug("  length = {0}", answerLength);
      this.LogDebug("  blind  = {0}", blind);
      lock (_caMenuCallBackLock)
      {
        if (_caMenuCallBacks != null)
        {
          _caMenuCallBacks.OnCiRequest(blind, answerLength, prompt);
        }
        else
        {
          this.LogDebug("KNC: menu call backs are not set");
        }
      }
    }

    /// <summary>
    /// Called by the tuner driver when the CAM wants to close the menu.
    /// </summary>
    /// <param name="slotIndex">The index of the CI slot containing the CAM.</param>
    /// <param name="delay">The delay (in milliseconds) after which the menu should be closed.</param>
    /// <param name="context">The optional context passed to the interface when the interface was opened.</param>
    private void OnCiCloseDisplay(byte slotIndex, uint delay, IntPtr context)
    {
      this.LogInfo("KNC: device {0} close menu call back, slot = {1}, delay = {2}", _deviceIndex, slotIndex, delay);
      lock (_caMenuCallBackLock)
      {
        if (_caMenuCallBacks != null)
        {
          _caMenuCallBacks.OnCiCloseDisplay((int)delay);
        }
        else
        {
          this.LogDebug("KNC: menu call backs are not set");
        }
      }
    }

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// The loading priority for this extension.
    /// </summary>
    public override byte Priority
    {
      get
      {
        // KNC and Omicom tuners implement the same property set for DiSEqC support. However KNC tuners may
        // implement further extended functionality (namely conditional access support). In order to ensure
        // that the full tuner functionality is available we want to ensure that the KNC class is used
        // in preference to the Omicom class if appropriate.
        return 60;
      }
    }

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        return _name;
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalIdentifier">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalIdentifier, CardType tunerType, object context)
    {
      this.LogDebug("KNC: initialising");

      IBaseFilter tunerFilter = context as IBaseFilter;
      if (tunerFilter == null)
      {
        this.LogDebug("KNC: tuner filter is null");
        return false;
      }
      if (string.IsNullOrEmpty(tunerExternalIdentifier))
      {
        this.LogDebug("KNC: tuner external identifier is not set");
        return false;
      }
      if (_isKnc)
      {
        this.LogWarn("KNC: extension already initialised");
        return true;
      }

      // Stage 1: check if this tuner is supported by the KNC API.
      FilterInfo tunerInfo;
      int hr = tunerFilter.QueryFilterInfo(out tunerInfo);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("KNC: failed to get the tuner filter name, hr = 0x{0:x} - {1}", HResult.GetDXErrorString(hr));
        return false;
      }
      foreach (string validTunerName in VALID_TUNER_NAMES)
      {
        if (tunerInfo.achName.Equals(validTunerName))
        {
          this.LogDebug("KNC: recognised filter name \"{0}\"", tunerInfo.achName);
          _name = validTunerName.Substring(0, validTunerName.IndexOf(' '));

          // Stage 2: attempt to get the KNC device index corresponding with this tuner.
          string devicePath = tunerExternalIdentifier.ToLowerInvariant();
          _deviceIndex = GetDeviceIndex(devicePath);
          if (_deviceIndex < 0)
          {
            this.LogError("KNC: failed to calculate the device index");
            break;
          }

          // Stage 3: ensure we can get a reference to the filter that implements the proprietary
          // interfaces.
          this.LogDebug("KNC: device index is {0}", _deviceIndex);
          if (!devicePath.Contains("dev_7160"))
          {
            // This is a PCI device. The tuner filter implements the proprietary interfaces so no further
            // work is required.
            _isKnc = true;
            break;
          }

          // This is a PCIe device. We need a reference to the capture filter because that is the filter
          // which implements the proprietary interfaces.
          this.LogDebug("KNC: this is a PCIe device");
          _isPcie = true;
          _name = "KNC";

          IPin tunerOutputPin = DsFindPin.ByDirection(tunerFilter, PinDirection.Output, 0);
          IPin captureInputPin;
          hr = tunerOutputPin.ConnectedTo(out captureInputPin);
          Release.ComObject("KNC tuner filter output pin", ref tunerOutputPin);
          if (hr != (int)HResult.Severity.Success || captureInputPin == null)
          {
            this.LogError("KNC: failed to get the capture filter input pin, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
            break;
          }

          PinInfo captureInfo;
          hr = captureInputPin.QueryPinInfo(out captureInfo);
          Release.ComObject("KNC capture filter input pin", ref captureInputPin);
          if (hr != (int)HResult.Severity.Success || captureInfo.filter == null)
          {
            this.LogError("KNC: failed to get the capture filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
          }
          else
          {
            _isKnc = true;
            _captureFilter = captureInfo.filter;
          }
          break;
        }
      }

      if (!_isKnc)
      {
        this.LogDebug("KNC: extension not supported");
        Release.FilterInfo(ref tunerInfo);
        return false;
      }

      this.LogInfo("KNC: extension supported");
      _diseqcBuffer = Marshal.AllocCoTaskMem(MAX_DISEQC_COMMAND_LENGTH);
      _tunerFilter = tunerFilter;
      _graph = (IFilterGraph2)tunerInfo.pGraph;

      // Prepare the hardware interface for use. This seems to always succeed...
      if (!KNCBDA_HW_Enable(_deviceIndex, _isPcie ? _captureFilter : _tunerFilter))
      {
        this.LogError("KNC: failed to enable the hardware");
      }
      return true;
    }

    #region device state change call backs

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITVCard tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("KNC: on before tune call back");
      action = TunerAction.Default;

      if (!_isKnc)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return;
      }

      // We only need to tweak parameters for DVB-S/S2 channels.
      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }

      // The KNC TV-Station DVB-S2 seems to have trouble working out which local oscillator frequency to use (and
      // hence, whether to turn the 22 kHz tone on or off) for some LNB settings. This could be to do with the use
      // of 18000000 kHz for the LNB switch frequency for single oscillator LNBs. The workaround is to set the two
      // LOFs to the single, correct LOF value when we want the 22 kHz tone to be turned off.
      if (ch.Frequency <= ch.LnbType.SwitchFrequency)
      {
        // Note: this logic does handle bandstacked LNBs properly.
        ch.LnbType.HighBandFrequency = ch.LnbType.LowBandFrequency;
        this.LogDebug("  LNB LOF    = {0}", ch.LnbType.LowBandFrequency);
      }

      if (ch.ModulationType == ModulationType.ModQpsk || ch.ModulationType == ModulationType.Mod8Psk)
      {
        // The KNC PCIe tuner driver behaves differently to the PCI tuner driver.
        if (_isPcie)
        {
          if (ch.ModulationType == ModulationType.ModQpsk)
          {
            ch.ModulationType = ModulationType.ModNbcQpsk;
          }
          else
          {
            ch.ModulationType = ModulationType.ModNbc8Psk;
          }
        }
        else
        {
          ch.ModulationType = ModulationType.Mod8Psk;
        }
      }
      else if (ch.ModulationType == ModulationType.ModNotSet)
      {
        ch.ModulationType = ModulationType.ModQpsk;
      }
      this.LogDebug("  modulation = {0}", ch.ModulationType);
    }

    #endregion

    #endregion

    #region IConditionalAccessProvider members

    /// <summary>
    /// Open the conditional access interface. For the interface to be opened successfully it is expected
    /// that any necessary hardware (such as a CI slot) is connected.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenInterface()
    {
      this.LogDebug("KNC: device {0} open conditional access interface", _deviceIndex);

      if (!_isKnc)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: interface is already open");
        return true;
      }

      _ciCallBacks = new KncCiCallBacks();
      _ciCallBacks.Context = IntPtr.Zero;
      _ciCallBacks.OnCiMenu = OnCiMenu;
      _ciCallBacks.OnCiMenuEntry = OnCiMenuEntry;
      _ciCallBacks.OnCiState = OnCiState;
      _ciCallBacks.OnCloseDisplay = OnCiCloseDisplay;
      _ciCallBacks.OnOpenDisplay = OnCiOpenDisplay;
      _ciCallBacks.OnRequest = OnCiRequest;
      _callBackBuffer = Marshal.AllocCoTaskMem(CALLBACK_SET_SIZE);
      Marshal.StructureToPtr(_ciCallBacks, _callBackBuffer, true);

      // Open the conditional access interface.
      bool result = false;
      if (_isPcie)
      {
        result = KNCBDA_CI_Enable(_deviceIndex, _graph, _captureFilter, _callBackBuffer);
      }
      else
      {
        result = KNCBDA_CI_Enable(_deviceIndex, null, _tunerFilter, _callBackBuffer);
      }
      if (!result)
      {
        this.LogError("KNC: CI enable failed");
        return false;
      }

      // Prepare the conditional access interface for use. This seems to always succeed...
      if (!KNCBDA_CI_HW_Enable(_deviceIndex, true))
      {
        this.LogError("KNC: CI HW enable failed");
        return false;
      }

      this.LogDebug("KNC: CI interface opened successfully");

      // Check if the CAM is currently present and ready for interaction. CI state change call backs will
      // tell us if the state changes.
      _isCamPresent = KNCBDA_CI_IsAvailable(_deviceIndex);
      if (_isCamPresent)
      {
        _isCamReady = KNCBDA_CI_IsReady(_deviceIndex);
        if (_isCamReady)
        {
          StringBuilder nameBuffer = new StringBuilder(100);
          if (KNCBDA_CI_GetName(_deviceIndex, nameBuffer, (uint)nameBuffer.MaxCapacity))
          {
            this.LogDebug("KNC: CAM name/type is {0}", nameBuffer);
          }
          else
          {
            this.LogWarn("KNC: failed to get the CAM name/type");
          }
        }
      }
      _isCaInterfaceOpen = true;

      this.LogDebug("KNC: result = success");
      return true;
    }

    /// <summary>
    /// Close the conditional access interface.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseInterface()
    {
      this.LogDebug("KNC: device {0} close conditional access interface", _deviceIndex);

      bool result = true;
      if (_callBackBuffer != IntPtr.Zero)
      {
        if (!KNCBDA_CI_Disable(_deviceIndex))
        {
          this.LogWarn("KNC: CI disable failed");
          result = false;
        }
        Marshal.FreeCoTaskMem(_callBackBuffer);
        _callBackBuffer = IntPtr.Zero;
      }

      _isCamPresent = false;
      _isCamReady = false;
      _ciState = KncCiState.Releasing;

      if (result)
      {
        this.LogDebug("KNC: result = success");
        _isCaInterfaceOpen = false;
        return true;
      }

      this.LogDebug("KNC: result = failure");
      return false;
    }

    /// <summary>
    /// Reset the conditional access interface.
    /// </summary>
    /// <param name="resetTuner">This parameter will be set to <c>true</c> if the tuner must be reset
    ///   for the interface to be completely and successfully reset.</param>
    /// <returns><c>true</c> if the interface is successfully reset, otherwise <c>false</c></returns>
    public bool ResetInterface(out bool resetTuner)
    {
      resetTuner = false;
      return CloseInterface() && OpenInterface();
    }

    /// <summary>
    /// Determine whether the conditional access interface is ready to receive commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is ready, otherwise <c>false</c></returns>
    public bool IsInterfaceReady()
    {
      this.LogDebug("KNC: is conditional access interface ready");

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }

      if (!KNCBDA_CI_IsAvailable(_deviceIndex))
      {
        this.LogDebug("KNC: CI slot or CAM not present");
        return false;
      }

      bool isCamReady = KNCBDA_CI_IsReady(_deviceIndex);
      this.LogDebug("KNC: result = {0}", isCamReady);
      return isCamReady;
    }

    /// <summary>
    /// Send a command to to the conditional access interface.
    /// </summary>
    /// <param name="channel">The channel information associated with the service which the command relates to.</param>
    /// <param name="listAction">It is assumed that the interface may be able to decrypt one or more services
    ///   simultaneously. This parameter gives the interface an indication of the number of services that it
    ///   will be expected to manage.</param>
    /// <param name="command">The type of command.</param>
    /// <param name="pmt">The program map table for the service.</param>
    /// <param name="cat">The conditional access table for the service.</param>
    /// <returns><c>true</c> if the command is successfully sent, otherwise <c>false</c></returns>
    public bool SendCommand(IChannel channel, CaPmtListManagementAction listAction, CaPmtCommand command, Pmt pmt, Cat cat)
    {
      this.LogDebug("KNC: send conditional access command, list action = {0}, command = {1}", listAction, command);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("KNC: the CAM is not ready");
        return false;
      }
      if (pmt == null)
      {
        this.LogError("KNC: PMT not supplied");
        return true;
      }

      // KNC supports the standard CA PMT format.
      byte[] caPmt = pmt.GetCaPmt(listAction, command);

      // Send the data to the CAM. Use a local buffer since PMT updates are asynchronous.
      IntPtr pmtBuffer = Marshal.AllocCoTaskMem(caPmt.Length);
      try
      {
        Marshal.Copy(caPmt, 0, pmtBuffer, caPmt.Length);
        //Dump.DumpBinary(pmtBuffer, caPmtLength);
        bool result = KNCBDA_CI_SendPMTCommand(_deviceIndex, pmtBuffer, (uint)caPmt.Length);
        this.LogDebug("KNC: result = {0}", result);
        return result;
      }
      finally
      {
        Marshal.FreeCoTaskMem(pmtBuffer);
        pmtBuffer = IntPtr.Zero;
      }
    }

    #endregion

    #region IConditionalAccessMenuActions members

    /// <summary>
    /// Set the menu call back delegate.
    /// </summary>
    /// <param name="callBacks">The call back delegate.</param>
    public void SetCallBacks(IConditionalAccessMenuCallBacks callBacks)
    {
      lock (_caMenuCallBackLock)
      {
        _caMenuCallBacks = callBacks;
      }
    }

    /// <summary>
    /// Send a request from the user to the CAM to open the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool EnterMenu()
    {
      this.LogDebug("KNC: device {0} slot {1} enter menu", _deviceIndex, _slotIndex);
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("KNC: the CAM is not ready");
        return false;
      }
      return KNCBDA_CI_EnterMenu(_deviceIndex, _slotIndex);
    }

    /// <summary>
    /// Send a request from the user to the CAM to close the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool CloseMenu()
    {
      this.LogDebug("KNC: device {0} slot {1} close menu", _deviceIndex, _slotIndex);
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("KNC: the CAM is not ready");
        return false;
      }
      return KNCBDA_CI_CloseMenu(_deviceIndex, _slotIndex);
    }

    /// <summary>
    /// Send a menu entry selection from the user to the CAM.
    /// </summary>
    /// <param name="choice">The index of the selection as an unsigned byte value.</param>
    /// <returns><c>true</c> if the selection is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool SelectMenuEntry(byte choice)
    {
      this.LogDebug("KNC: device {0} slot {1} select menu entry, choice = {2}", _deviceIndex, _slotIndex, choice);
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("KNC: the CAM is not ready");
        return false;
      }
      return KNCBDA_CI_SelectMenu(_deviceIndex, _slotIndex, choice);
    }

    /// <summary>
    /// Send an answer to an enquiry from the user to the CAM.
    /// </summary>
    /// <param name="cancel"><c>True</c> to cancel the enquiry.</param>
    /// <param name="answer">The user's answer to the enquiry.</param>
    /// <returns><c>true</c> if the answer is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool AnswerEnquiry(bool cancel, string answer)
    {
      if (answer == null)
      {
        answer = string.Empty;
      }
      this.LogDebug("KNC: device {0} slot {1} answer enquiry, answer = {2}, cancel = {3}", _deviceIndex, _slotIndex, answer, cancel);
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("KNC: the CAM is not ready");
        return false;
      }
      return KNCBDA_CI_SendMenuAnswer(_deviceIndex, _slotIndex, cancel, answer);
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Send a tone/data burst command, and then set the 22 kHz continuous tone state.
    /// </summary>
    /// <remarks>
    /// The KNC interface does not support sending tone burst commands, and the 22 kHz tone state
    /// cannot be set directly. The tuning request LNB frequency parameters can be used to manipulate the
    /// tone state appropriately.
    /// </remarks>
    /// <param name="toneBurstState">The tone/data burst command to send, if any.</param>
    /// <param name="tone22kState">The 22 kHz continuous tone state to set.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      // Not supported.
      return false;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendCommand(byte[] command)
    {
      this.LogDebug("KNC: device {0} send DiSEqC command", _deviceIndex);

      if (!_isKnc)
      {
        this.LogWarn("KNC: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogError("KNC: command not supplied");
        return true;
      }

      int length = command.Length;
      if (length > MAX_DISEQC_COMMAND_LENGTH)
      {
        this.LogError("KNC: command too long, length = {0}", command.Length);
        return false;
      }

      Marshal.Copy(command, 0, _diseqcBuffer, length);
      //Dump.DumpBinary(_diseqcBuffer, length);

      bool success = KNCBDA_HW_DiSEqCWrite(_deviceIndex, _diseqcBuffer, (uint)length, 0);
      this.LogDebug("KNC: result = {0}", success);
      return success;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadResponse(out byte[] response)
    {
      // Not supported.
      response = null;
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      CloseInterface();
      if (_diseqcBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_diseqcBuffer);
        _diseqcBuffer = IntPtr.Zero;
      }
      if (_callBackBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_callBackBuffer);
        _callBackBuffer = IntPtr.Zero;
      }
      Release.ComObject("KNC graph", ref _graph);
      Release.ComObject("KNC capture filter", ref _captureFilter);
      _tunerFilter = null;
      _isKnc = false;
    }

    #endregion
  }
}