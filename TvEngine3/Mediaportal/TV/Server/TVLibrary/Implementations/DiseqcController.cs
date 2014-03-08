#region Copyright (C) 2005-2011 Team MediaPortal

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
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.TVLibrary.Implementations
{
  /// <summary>
  /// A controller class for DiSEqC devices. This controller is able to control positioners and
  /// switches.
  /// </summary>
  public class DiseqcController : IDiseqcController
  {
    #region variables

    private IDiseqcDevice _device = null;
    private DVBSChannel _previousChannel = null;

    /// <summary>
    /// Enable or disable always sending DiSEqC commands.
    /// </summary>
    /// <remarks>
    /// DiSEqC commands are usually only sent when changing to a channel on a different switch port
    /// or at a different positioner location. Enabling this option will cause DiSEqC commands to
    /// be sent on each channel change. My experiments with a wide variety of tuners suggest that
    /// only TeVii tuners require this setting to be enabled.
    /// </remarks>
    private bool _alwaysSendCommands = false;

    /// <summary>
    /// The number of times to repeat DiSEqC commands.
    /// </summary>
    /// <remarks>
    /// When set to zero, commands are sent once; when set to one, commands are sent twice... etc.
    /// </remarks>
    private int _repeatCount = 0;

    private int _commandDelay = 100;
    private int _currentPosition = -1;  // Ensure that we always send motor commands on first tune.
    private int _currentStepsAzimuth;
    private int _currentStepsElevation;

    #endregion

    /// <summary>
    /// Initialise a new instance of the <see cref="DiseqcController"/> class.
    /// </summary>
    /// <param name="device">A tuner's DiSEqC control interface.</param>
    public DiseqcController(IDiseqcDevice device)
    {
      _device = device;
      if (device == null)
      {
        throw new ArgumentException("DiSEqC: device is null");
      }
    }

    /// <summary>
    /// Reload the controller's configuration.
    /// </summary>
    /// <param name="tunerId">The identifier for the associated tuner.</param>
    public void ReloadConfiguration(int tunerId)
    {
      Card tuner = CardManagement.GetCard(tunerId, CardIncludeRelationEnum.None);
      if (tuner != null)
      {
        _alwaysSendCommands = tuner.AlwaysSendDiseqcCommands;
        _repeatCount = tuner.DiseqcCommandRepeatCount;
        if (_repeatCount > 5)
        {
          // It would be rare that commands would need to be repeated more than twice. Five times
          // is a more than reasonable practical limit.
          _repeatCount = 5;
        }
        _commandDelay = SettingsManagement.GetValue("tuner" + tunerId + "DiseqcCommandDelay", 100);
      }
      else
      {
        _alwaysSendCommands = false;
        _repeatCount = 0;
        _commandDelay = 100;
      }
    }

    /// <summary>
    /// Reset a device's microcontroller.
    /// </summary>
    public void Reset()
    {
      // Note: we don't assume that the power supply is on to begin with. I don't know why
      // the reset is not applied to "any" device, but it might be to prevent loosing all
      // stored positions in a positioner or something like that.
      byte[] cmd = new byte[3];
      this.LogDebug("DiSEqC: clear reset");
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnySwitch;
      cmd[2] = (byte)DiseqcCommand.ClearReset;
      _device.SendCommand(cmd);
      System.Threading.Thread.Sleep(_commandDelay);

      this.LogDebug("DiSEqC: power on");
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnySwitch;
      cmd[2] = (byte)DiseqcCommand.PowerOn;
      _device.SendCommand(cmd);
      System.Threading.Thread.Sleep(_commandDelay);

      this.LogDebug("DiSEqC: reset");
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnySwitch;
      cmd[2] = (byte)DiseqcCommand.Reset;
      _device.SendCommand(cmd);
      System.Threading.Thread.Sleep(_commandDelay);

      this.LogDebug("DiSEqC: clear reset");
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnySwitch;
      cmd[2] = (byte)DiseqcCommand.ClearReset;
      _device.SendCommand(cmd);
      System.Threading.Thread.Sleep(_commandDelay);

      this.LogDebug("DiSEqC: power on");
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnySwitch;
      cmd[2] = (byte)DiseqcCommand.PowerOn;
      _device.SendCommand(cmd);
    }

    /// <summary>
    /// Send the required switch and positioner command(s) to tune a given channel.
    /// </summary>
    /// <param name="channel">The channel to tune.</param>
    public void SwitchToChannel(DVBSChannel channel)
    {
      // If the channel is not set, this is a request for the controller to forget the previously tuned
      // channel. This will force commands to be sent on the next call to SwitchToChannel().
      if (channel == null)
      {
        _previousChannel = null;
        return;
      }

      // There is a well defined order in which commands may be sent:
      // "raw" DiSEqC commands -> DiSEqC 1.0 (committed) -> tone burst (simple DiSEqC) -> 22 kHz tone on/off
      this.LogDebug("DiSEqC: switch to channel");

      // We send a "power on" command before anything else if the previous channel is not set. This is
      // sometimes necessary to wake the switch.
      if (_previousChannel == null)
      {
        this.LogDebug("DiSEqC: power on");
        byte[] command = new byte[3];
        command[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
        command[1] = (byte)DiseqcAddress.Any;
        command[2] = (byte)DiseqcCommand.PowerOn;
        _device.SendCommand(command);
        // Give DiSEqC devices time to boot up.
        System.Threading.Thread.Sleep(_commandDelay);
      }

      bool isHighBand = channel.Frequency > channel.LnbType.SwitchFrequency && channel.LnbType.SwitchFrequency > 0;

      // Switch command.
      bool sendCommand = channel.Diseqc != DiseqcPort.None &&
        channel.Diseqc != DiseqcPort.SimpleA &&
        channel.Diseqc != DiseqcPort.SimpleB;
      if (sendCommand)
      {
        bool wasHighBand = !isHighBand;
        if (_previousChannel != null)
        {
          wasHighBand = _previousChannel.Frequency > _previousChannel.LnbType.SwitchFrequency && _previousChannel.LnbType.SwitchFrequency > 0;
        }

        // If we get to here then there is a valid command to send, but we might not need/want to send it.
        if (!_alwaysSendCommands &&
          _previousChannel != null &&
          _previousChannel.Diseqc == channel.Diseqc &&
          (
            (channel.Diseqc != DiseqcPort.PortA &&
            channel.Diseqc != DiseqcPort.PortB &&
            channel.Diseqc != DiseqcPort.PortC &&
            channel.Diseqc != DiseqcPort.PortD)
            ||
            (_previousChannel.Polarisation == channel.Polarisation &&
            wasHighBand == isHighBand)
          )
        )
        {
          sendCommand = false;
        }
      }
      if (!sendCommand)
      {
        this.LogDebug("DiSEqC: no need to send switch command");
      }
      else
      {
        byte[] command = new byte[4];
        command[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
        command[1] = (byte)DiseqcAddress.AnySwitch;
        command[3] = 0xf0;
        int portNumber = GetPortNumber(channel.Diseqc);
        if (channel.Diseqc == DiseqcPort.PortA ||
          channel.Diseqc == DiseqcPort.PortB ||
          channel.Diseqc == DiseqcPort.PortC ||
          channel.Diseqc == DiseqcPort.PortD)
        {
          this.LogDebug("DiSEqC: DiSEqC 1.0 switch command");
          command[2] = (byte)DiseqcCommand.WriteN0;
          bool isHorizontal = channel.Polarisation == Polarisation.LinearH || channel.Polarisation == Polarisation.CircularL;
          command[3] |= (byte)(isHighBand ? 1 : 0);
          command[3] |= (byte)((isHorizontal) ? 2 : 0);
          command[3] |= (byte)((portNumber - 1) << 2);
        }
        else
        {
          this.LogDebug("DiSEqC: DiSEqC 1.1 switch command");
          command[2] = (byte)DiseqcCommand.WriteN1;
          command[3] |= (byte)(portNumber - 1);
        }
        _device.SendCommand(command);
        Repeat(command);
      }

      // Positioner movement.
      sendCommand = channel.SatelliteIndex > 0;
      if (sendCommand)
      {
        if (!_alwaysSendCommands &&
          _currentStepsAzimuth == 0 &&
          _currentStepsElevation == 0 &&
          channel.SatelliteIndex == _currentPosition)
        {
          sendCommand = false;
        }
      }
      if (!sendCommand)
      {
        this.LogDebug("DiSEqC: no need to send positioner command");
      }
      else
      {
        this.LogDebug("DiSEqC: positioner command(s)");
        GotoPosition((byte)channel.SatelliteIndex);
      }

      // Tone burst and final state.
      ToneBurst toneBurst = ToneBurst.None;
      if (channel.Diseqc == DiseqcPort.SimpleA)
      {
        toneBurst = ToneBurst.ToneBurst;
      }
      else if (channel.Diseqc == DiseqcPort.SimpleB)
      {
        toneBurst = ToneBurst.DataBurst;
      }
      Tone22k tone22k = Tone22k.Off;
      if (isHighBand)
      {
        tone22k = Tone22k.On;
      }
      _device.SetToneState(toneBurst, tone22k);

      _previousChannel = channel;
    }

    /// <summary>
    /// Get the switch port number (or LNB number) for a given DiSEqC switch command.
    /// </summary>
    /// <param name="command">The DiSEqC switch command.</param>
    /// <returns>the switch port number associated with the command</returns>
    public static int GetPortNumber(DiseqcPort command)
    {
      switch (command)
      {
        case DiseqcPort.None:
          return 0;   // no DiSEqC
        case DiseqcPort.SimpleA:
          return 1;
        case DiseqcPort.SimpleB:
          return 2;
        case DiseqcPort.PortA:
          return 1;
        case DiseqcPort.PortB:
          return 2;
        case DiseqcPort.PortC:
          return 3;
        case DiseqcPort.PortD:
          return 4;
      }
      // DiSEqC 1.1 commands...
      return ((int)command - 6);
    }

    /// <summary>
    /// Repeat a given command for the configured number of repeats with the configured command delay.
    /// </summary>
    /// <param name="command">The command to repeat.</param>
    private void Repeat(byte[] command)
    {
      command[0] = (byte)DiseqcFrame.CommandRepeatTransmissionNoReply;
      for (int i = 0; i < _repeatCount; i++)
      {
        System.Threading.Thread.Sleep(_commandDelay);
        this.LogDebug("  repeat {0}...", i + 1);
        _device.SendCommand(command);
      }
    }

    #region positioner (motor) control

    /// <summary>
    /// Stop the movement of a positioner device.
    /// </summary>
    public void Stop()
    {
      this.LogDebug("DiSEqC: stop positioner");
      byte[] cmd = new byte[3];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AnyPositioner;
      cmd[2] = (byte)DiseqcCommand.Halt;
      _device.SendCommand(cmd);
      Repeat(cmd);
    }

    /// <summary>
    /// Set the Eastward soft-limit of movement for a positioner device.
    /// </summary>
    public void SetEastLimit()
    {
      this.LogDebug("DiSEqC: set east limit");
      byte[] cmd = new byte[3];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
      cmd[2] = (byte)DiseqcCommand.LimitEast;
      _device.SendCommand(cmd);
      Repeat(cmd);
    }

    /// <summary>
    /// Set the Westward soft-limit of movement for a positioner device.
    /// </summary>
    public void SetWestLimit()
    {
      this.LogDebug("DiSEqC: set west limit");
      byte[] cmd = new byte[3];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
      cmd[2] = (byte)DiseqcCommand.LimitWest;
      _device.SendCommand(cmd);
      Repeat(cmd);
    }

    /// <summary>
    /// Enable/disable the movement soft-limits for a positioner device.
    /// </summary>
    public bool ForceLimits
    {
      set
      {
        if (value)
        {
          this.LogDebug("DiSEqC: enable limits");
          byte[] cmd = new byte[4];
          cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
          cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
          cmd[2] = (byte)DiseqcCommand.StorePosition;
          cmd[3] = 0;
          _device.SendCommand(cmd);
          Repeat(cmd);
        }
        else
        {
          this.LogDebug("DiSEqC: disable limits");
          byte[] cmd = new byte[3];
          cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
          cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
          cmd[2] = (byte)DiseqcCommand.LimitsOff;
          _device.SendCommand(cmd);
          Repeat(cmd);
        }
      }
    }

    /// <summary>
    /// Drive a positioner device in a given direction for a specified period of time.
    /// </summary>
    /// <param name="direction">The direction to move in.</param>
    /// <param name="steps">The number of position steps to move.</param>
    public void DriveMotor(DiseqcDirection direction, byte steps)
    {
      this.LogDebug("DiSEqC: drive motor {0} for {1} steps", direction.ToString(), steps);
      if (steps == 0)
      {
        return;
      }

      Stop();
      byte[] cmd = new byte[4];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      if (direction == DiseqcDirection.West)
      {
        cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
        cmd[2] = (byte)DiseqcCommand.DriveWest;
        _currentStepsAzimuth -= steps;
      }
      else if (direction == DiseqcDirection.East)
      {
        cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
        cmd[2] = (byte)DiseqcCommand.DriveEast;
        _currentStepsAzimuth += steps;
      }
      else if (direction == DiseqcDirection.Up)
      {
        cmd[1] = (byte)DiseqcAddress.ElevationPositioner;
        cmd[2] = (byte)DiseqcCommand.DriveWest;
        _currentStepsElevation -= steps;
      }
      else if (direction == DiseqcDirection.Down)
      {
        cmd[1] = (byte)DiseqcAddress.ElevationPositioner;
        cmd[2] = (byte)DiseqcCommand.DriveEast;
        _currentStepsElevation += steps;
      }
      cmd[3] = (byte)(0x100 - steps);
      _device.SendCommand(cmd);
      Repeat(cmd);
      //System.Threading.Thread.Sleep(1000*steps);
      //StopMotor();
    }

    /// <summary>
    /// Store the current position of a positioner device for later use.
    /// </summary>
    /// <param name="position">The identifier to use for the position.</param>
    public void StorePosition(byte position)
    {
      this.LogDebug("DiSEqC: store current position as position {0}", position);
      if (position <= 0)
      {
        throw new ArgumentException("DiSEqC: position cannot be less than or equal to zero");
      }

      byte[] cmd = new byte[4];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
      cmd[2] = (byte)DiseqcCommand.StorePosition;
      cmd[3] = position;
      _device.SendCommand(cmd);
      Repeat(cmd);

      // The current position becomes our reference.
      _currentPosition = position;
      _currentStepsAzimuth = 0;
      _currentStepsElevation = 0;
    }

    /// <summary>
    /// Drive a positioner device to its reference position.
    /// </summary>
    public void GotoReferencePosition()
    {
      this.LogDebug("DiSEqC: go to reference position");

      byte[] cmd = new byte[4];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
      cmd[2] = (byte)DiseqcCommand.GotoPosition;
      cmd[3] = 0;
      _device.SendCommand(cmd);
      Repeat(cmd);

      // The current position becomes our reference.
      _currentPosition = 0;
      _currentStepsAzimuth = 0;
      _currentStepsElevation = 0;
    }

    /// <summary>
    /// Drive a positioner device to a previously stored position.
    /// </summary>
    /// <param name="position">The position to drive to.</param>
    public void GotoPosition(byte position)
    {
      this.LogDebug("DiSEqC: go to position {0}", position);
      if (position <= 0)
      {
        throw new ArgumentException("DiSEqC: position cannot be less than or equal to zero");
      }

      byte[] cmd = new byte[4];
      cmd[0] = (byte)DiseqcFrame.CommandFirstTransmissionNoReply;
      cmd[1] = (byte)DiseqcAddress.AzimuthPositioner;
      cmd[2] = (byte)DiseqcCommand.GotoPosition;
      cmd[3] = position;
      _device.SendCommand(cmd);
      Repeat(cmd);

      // The current position becomes our reference.
      _currentPosition = position;
      _currentStepsAzimuth = 0;
      _currentStepsElevation = 0;
    }

    /// <summary>
    /// Get the current position of a positioner device.
    /// </summary>
    /// <param name="satellitePosition">The stored position number corresponding with the current position.</param>
    /// <param name="stepsAzimuth">The number of steps taken from the position on the azmutal axis.</param>
    /// <param name="stepsElevation">The number of steps taken from the position on the vertical (elevation) axis.</param>
    public void GetPosition(out int satellitePosition, out int stepsAzimuth, out int stepsElevation)
    {
      satellitePosition = _currentPosition;
      stepsAzimuth = _currentStepsAzimuth;
      stepsElevation = _currentStepsElevation;
    }

    #endregion
  }
}