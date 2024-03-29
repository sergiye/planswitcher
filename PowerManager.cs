﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PlanSwitcher {

  public class PowerPlan {
    public readonly string name;
    public Guid guid;

    public PowerPlan(string name, Guid guid) {
      this.name = name;
      this.guid = guid;
    }
  }

  public interface IPowerManager {
    /// <returns>
    /// All supported power plans.
    /// </returns>
    List<PowerPlan> GetPlans();

    bool IsCharging();

    PowerPlan GetCurrentPlan();

    /// <returns>Battery charge value in percent, 
    /// i.e. values in a 0..100 range</returns>
    int GetChargeValue();

    void SetActive(PowerPlan plan);

    /// <summary>
    /// Opens Power Options section of the Control Panel.
    /// </summary>
    void OpenControlPanel();
  }

  public class PowerManagerProvider {
    public static IPowerManager CreatePowerManager(UICallback uiCallback) {
      return new PowerManager(uiCallback);
    }
  }

  class PowerManager : IPowerManager {
    /// <summary>
    /// Indicates that almost no power savings measures will be used.
    /// </summary>
    private readonly PowerPlan MaximumPerformance;

    /// <summary>
    /// Indicates that fairly aggressive power savings measures will be used.
    /// </summary>
    private readonly PowerPlan Balanced;

    /// <summary>
    /// Indicates that very aggressive power savings measures will be used to help
    /// stretch battery life.                                                     
    /// </summary>
    private readonly PowerPlan PowerSourceOptimized;

    private UICallback uiCallback;

    public PowerManager(UICallback uiCallback) {
      this.uiCallback = uiCallback;

      // See GUID values in WinNT.h.
      MaximumPerformance = NewPlan("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
      Balanced = NewPlan("381b4222-f694-41f0-9685-ff5bb260df2e");
      PowerSourceOptimized = NewPlan("a1841308-3541-4fab-bc81-f71556f20b4a");

      // Add handler for power mode state changing.
      Microsoft.Win32.SystemEvents.PowerModeChanged += new Microsoft.Win32.PowerModeChangedEventHandler(PowerModeChangedHandler);
    }

    private PowerPlan NewPlan(string guidString) {
      Guid guid = new Guid(guidString);
      return new PowerPlan(GetPowerPlanName(guid), guid);
    }

    public void SetActive(PowerPlan plan) {
      PowerSetActiveScheme(IntPtr.Zero, ref plan.guid);
      uiCallback.UpdateBatteryState();

      //Logger.Instance().Info("Switched to " + plan.name);
    }

    /// <returns>
    /// All supported power plans.
    /// </returns>
    public List<PowerPlan> GetPlans() {
      return new List<PowerPlan>(new PowerPlan[] {
                MaximumPerformance,
                Balanced,
                PowerSourceOptimized
            });
    }

    public bool IsCharging() {
      PowerStatus pwrStatus = SystemInformation.PowerStatus;
      return pwrStatus.PowerLineStatus == PowerLineStatus.Online;
    }

    public int GetChargeValue() {
      PowerStatus pwrStatus = SystemInformation.PowerStatus;
      return (int)(pwrStatus.BatteryLifePercent * 100);
    }

    private Guid GetActiveGuid() {
      Guid ActiveScheme = Guid.Empty;
      IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
      if (PowerGetActiveScheme((IntPtr)null, out ptr) == 0) {
        ActiveScheme = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
        if (ptr != null) {
          Marshal.FreeHGlobal(ptr);
        }
      }
      return ActiveScheme;
    }

    public PowerPlan GetCurrentPlan() {
      Guid guid = GetActiveGuid();
      return GetPlans().Find(p => (p.guid == guid));
    }

    private void PowerModeChangedHandler(object sender, EventArgs e) {
      if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online) {
        SetActive(MaximumPerformance);
        //Logger.Instance().Info("Power connected");
      }
      else if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline) {
        SetActive(PowerSourceOptimized);
        //Logger.Instance().Info("Power disconnected");
      }
      else {
        //Logger.Instance().Warn("Power state changed to an unknown value");
      }
    }

    private static string GetPowerPlanName(Guid guid) {
      string name = string.Empty;
      IntPtr lpszName = (IntPtr)null;
      uint dwSize = 0;

      PowerReadFriendlyName((IntPtr)null, ref guid, (IntPtr)null, (IntPtr)null, lpszName, ref dwSize);
      if (dwSize > 0) {
        lpszName = Marshal.AllocHGlobal((int)dwSize);
        if (0 == PowerReadFriendlyName((IntPtr)null, ref guid, (IntPtr)null, (IntPtr)null, lpszName, ref dwSize)) {
          name = Marshal.PtrToStringUni(lpszName);
        }
        if (lpszName != IntPtr.Zero)
          Marshal.FreeHGlobal(lpszName);
      }

      return name;
    }

    /// <summary>
    /// Opens Power Options section of the Control Panel.
    /// </summary>
    public void OpenControlPanel() {
      var root = Environment.GetEnvironmentVariable("SystemRoot");
      Process.Start(root + "\\system32\\control.exe", "/name Microsoft.PowerOptions");
    }

    #region DLL imports

    [DllImport("kernel32.dll")]
    private static extern int GetSystemDefaultLCID();

    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    public static extern uint PowerSetActiveScheme(IntPtr UserPowerKey, ref Guid ActivePolicyGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
    public static extern uint PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerReadFriendlyName")]
    public static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);

    #endregion
  }
}
