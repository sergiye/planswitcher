﻿using System;
using System.Windows.Forms;

namespace PlanSwitcher {

  public class KeyPressedEventArgs : EventArgs {

    internal KeyPressedEventArgs(ModifierKeys modifier, Keys key) {
      Modifier = modifier;
      Key = key;
    }

    public ModifierKeys Modifier { get; }
    public Keys Key { get; }
  }
}
