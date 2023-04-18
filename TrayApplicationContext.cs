using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;

namespace PlanSwitcher {

  public interface UICallback {
    void UpdateBatteryState();
  }

  public class TrayApplicationContext : ApplicationContext, UICallback {
    /// <summary>
    /// Power state update interval, milliseconds.
    /// </summary>
    private const int TimerInterval = 5 * 1000;

    /// <summary>
    /// The list of components to be disposed on context disposal.
    /// </summary>
    private readonly System.ComponentModel.IContainer components = new System.ComponentModel.Container();

    private NotifyIcon notifyIcon;

    private readonly IPowerManager powerManager;

    private readonly List<PowerPlan> plans;

    private readonly Timer refreshTimer;

    private readonly KeyboardHook hook = new KeyboardHook();

    public TrayApplicationContext() {
      InitializeContext();

      refreshTimer = new Timer(components) {
        Interval = TimerInterval
      };
      refreshTimer.Tick += new EventHandler(TimerHandler);
      refreshTimer.Enabled = true;

      powerManager = PowerManagerProvider.CreatePowerManager(this);
      plans = powerManager.GetPlans();

      AddMenuItems();
      UpdateBatteryState();

      hook.KeyPressed += new EventHandler<KeyPressedEventArgs>(SwitchToNextPlan);
      hook.RegisterHotKey(ModifierKeys.Control | ModifierKeys.Alt, Keys.Q);
    }

    private void InitializeContext() {
      notifyIcon = new NotifyIcon(components) {
        ContextMenuStrip = new ContextMenuStrip(),
        Icon = null,
        Text = "Power Plan manager",
        Visible = true
      };
      notifyIcon.ContextMenuStrip.Opening += OnContextMenuStripOpening;
      notifyIcon.MouseUp += OnNotifyIconMouseUp;
    }

    private void TimerHandler(object sender, EventArgs e) {
      UpdateBatteryState();
    }

    public void UpdateBatteryState() {
      PowerPlan currentPlan = powerManager.GetCurrentPlan();
      bool isCharging = powerManager.IsCharging();
      int percentValue = powerManager.GetChargeValue();

      UpdateBatteryState(currentPlan.name, isCharging, percentValue);
    }

    private void UpdateBatteryState(string planName, bool isCharging, int percentValue) {
      Icon icon;
      if (percentValue >= 86) {
        icon = isCharging ? Properties.Resources.batt_ch_4 : Properties.Resources.batt_4;
      }
      else if (percentValue >= 62) {
        icon = isCharging ? Properties.Resources.batt_ch_3 : Properties.Resources.batt_3;
      }
      else if (percentValue >= 38) {
        icon = isCharging ? Properties.Resources.batt_ch_2 : Properties.Resources.batt_2;
      }
      else if (percentValue >= 14) {
        icon = isCharging ? Properties.Resources.batt_ch_1 : Properties.Resources.batt_1;
      }
      else {
        icon = isCharging ? Properties.Resources.batt_ch_0 : Properties.Resources.batt_0;
      }

      notifyIcon.Icon = icon;
      notifyIcon.Text = planName + " (" + percentValue + "%)";
    }

    private void AddMenuItems() {

      ToolStripMenuItem item;
      // Add an item for each power plan.
      PowerPlan currentPlan = powerManager.GetCurrentPlan();
      foreach (PowerPlan p in plans) {
        item = new ToolStripMenuItem(p.name);
        PowerPlan pp = p;
        item.Click += delegate (object sender, EventArgs args) {
          powerManager.SetActive(pp);
        };
        item.Checked = (currentPlan == p);
        notifyIcon.ContextMenuStrip.Items.Add(item);
      }

      notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

      item = new ToolStripMenuItem("Switch to next plan");
      item.Click += SwitchToNextPlan;
      item.ShortcutKeys = Keys.Control | Keys.Alt | Keys.Q;
      item.ShowShortcutKeys = true;
      notifyIcon.ContextMenuStrip.Items.Add(item);

      item = new ToolStripMenuItem("Open control panel");
      item.Click += OpenControlPanelClick;
      notifyIcon.ContextMenuStrip.Items.Add(item);

      item = new ToolStripMenuItem("About");
      item.Click += (s, e)=> {
        try {
          Process.Start(new ProcessStartInfo("https://github.com/sergiye/planswitcher"));
        }
        catch { }
      };
      notifyIcon.ContextMenuStrip.Items.Add(item);

      item = new ToolStripMenuItem("Exit");
      item.Click += ExitClick;
      notifyIcon.ContextMenuStrip.Items.Add(item);
    }

    #region Interaction handlers

    private void OnContextMenuStripOpening(object sender, System.ComponentModel.CancelEventArgs e) {
      int idx = plans.IndexOf(powerManager.GetCurrentPlan());

      for (int i = 0; i < notifyIcon.ContextMenuStrip.Items.Count; i++) {
        var item = notifyIcon.ContextMenuStrip.Items[i];
        if (!(item is ToolStripMenuItem toolStripItem))
          break;
        toolStripItem.Checked = i == idx;
      }

    }

    private void OpenControlPanelClick(object sender, EventArgs e) {
      powerManager.OpenControlPanel();
    }

    private void OnNotifyIconMouseUp(object sender, MouseEventArgs e) {
      if (e.Button == MouseButtons.Left) {
        MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        mi.Invoke(notifyIcon, null);
      }
    }

    private void ExitClick(object sender, EventArgs e) {
      ExitThread();
    }

    private void SwitchToNextPlan(object sender, EventArgs e) {
      var idx = plans.IndexOf(powerManager.GetCurrentPlan());
      var nextIdx = idx == plans.Count - 1 ? 0 : idx + 1;
      powerManager.SetActive(plans[nextIdx]);
    }

    #endregion

    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
    }

    protected override void ExitThreadCore() {
      notifyIcon.Visible = false;
      base.ExitThreadCore();
    }
  }
}
