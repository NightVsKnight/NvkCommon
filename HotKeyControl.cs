using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static NvkCommon.Log;

namespace NvkCommon
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public partial class HotKeyControl : UserControl
    {
        private static readonly string TAG = NvkCommon.Log.TAG(typeof(HotKeyControl));

        public event EventHandler HotkeySequenceMatched;
        public event EventHandler HotkeySequenceRecorded;

        public List<RawInputCapture.KeyEventInfo> HotkeySequence
        {
            get
            {
                return hotkeySequenceActual;
            }
            set
            {
                hotkeySequenceActual.Clear();
                if (value != null)
                {
                    hotkeySequenceActual.AddRange(value);
                }
                HookStart();
            }
        }

        private bool HasKeyBinds
        {
            get
            {
                return hotkeySequenceActual.Count > 0;
            }
        }

        private bool _isKeyBindRecording;

        private bool IsKeyBindRecording
        {
            get
            {
                return _isKeyBindRecording;
            }
            set
            {
                if (value)
                {
                    if (!_isKeyBindRecording)
                    {
                        _isKeyBindRecording = true;
                        buttonPredefinedKeyBindAdd.Text = "*";
                        HotkeySequenceTextUpdate();
                        HookStart();
                    }
                }
                else
                {
                    if (_isKeyBindRecording)
                    {
                        _isKeyBindRecording = false;
                        buttonPredefinedKeyBindAdd.Text = "+";
                        HotkeySequenceTextUpdate();
                        if (!HasKeyBinds)
                        {
                            HookStop();
                        }
                    }
                }
            }
        }

        private readonly List<RawInputCapture.KeyEventInfo> hotkeySequenceActual = [];
        private readonly List<RawInputCapture.KeyEventInfo> hotkeySequencePrevious = [];
        private readonly List<RawInputCapture.KeyEventInfo> hotkeySequenceTest = [];

        private readonly RawInputCapture rawInputCapture;

        public HotKeyControl()
        {
            InitializeComponent();

            if (DesignMode) return;

            rawInputCapture = new RawInputCapture(Name);

            // BUG: revisit this; it is stopping the listener when the app deactivates
            // (which it should only do if there are no bindings),
            // and never resuming when the app re-activates. :/
            //ParentForm.Deactivate += ParentForm_Deactivate;
            //ParentForm.Activated += ParentForm_Activated;
        }

        /*
        ~HotKeyControl()
        {
            HookStop();
        }
        */

        public void WndProc(ref Message m)
        {
            rawInputCapture?.WndProc(ref m);
        }

        private void HotKeyControl_Load(object sender, EventArgs e)
        {
            textBoxPredefinedKeyBind.BackColor = Color.Green;
            textBoxPredefinedKeyBind.ForeColor = Color.White;

            HotkeySequenceTextUpdate();
        }

        private void HotkeySequenceTextUpdate()
        {
            if (IsKeyBindRecording)
            {
                if (HasKeyBinds)
                {
                    textBoxPredefinedKeyBind.Text = Utils.ToString(hotkeySequenceActual);
                }
                else
                {
                    textBoxPredefinedKeyBind.Text = "Recording...";
                }
                textBoxPredefinedKeyBind.BackColor = Color.Red;
                textBoxPredefinedKeyBind.ForeColor = Color.White;
            }
            else
            {
                if (HasKeyBinds)
                {
                    textBoxPredefinedKeyBind.Text = Utils.ToString(hotkeySequenceActual);
                    textBoxPredefinedKeyBind.BackColor = Color.Green;
                    textBoxPredefinedKeyBind.ForeColor = Color.White;
                }
                else
                {
                    textBoxPredefinedKeyBind.Text = "Press + to record";
                    textBoxPredefinedKeyBind.BackColor = Color.Yellow;
                    textBoxPredefinedKeyBind.ForeColor = Color.Black;
                }
            }
        }

        private void Log(LogLevel level, string message)
        {
            NvkCommon.Log.PrintLine(TAG, level, $"{Utils.Quote(Name)} {message}");
        }

        public void HookStart()
        {
            Log(LogLevel.Debug, $"HookStart()");
            if (!HasKeyBinds && !IsKeyBindRecording)
            {
                Log(LogLevel.Debug, $"HookStart: No key binds and not recording; ignoring");
                return;
            }
            if (rawInputCapture.IsCapturingKeyboard) return;
            Log(LogLevel.Debug, $"HookStart: HasKeyBinds || IsKeyBindRecording; starting");
            HotkeyListenerTestReset();
            rawInputCapture.OnKeyboard += RawInputCapture_OnKeyboard;
            rawInputCapture.StartKeyboard(FindForm().Handle);
        }

        public void HookStop()
        {
            Log(LogLevel.Debug, $"HookStop()");
            rawInputCapture.OnKeyboard -= RawInputCapture_OnKeyboard;
            HotkeyListenerTestReset();
            rawInputCapture.StopKeyboard(FindForm().Handle);
            if (IsKeyBindRecording)
            {
                IsKeyBindRecording = false;
            }
        }

        private void RawInputCapture_OnKeyboard(object sender, RawInputCapture.KeyboardEventArgs e)
        {
            //Log(LogLevel.Information, $"RawInputCapture_OnKeyboard: e={e}");
            //Log(LogLevel.Information, $"KeyboardHook_KeyIntercepted: e.KeyEventInfo={e.KeyEventInfo}");
            //Log(LogLevel.Information, $"KeyboardHook_KeyIntercepted: e.CapsLockOn={e.IsCapsLockOn}");
            var keyEventInfoActual = e.KeyEventInfo;
            //Log(LogLevel.Verbose, $"RawInputCapture_OnKeyboard:     keyEventInfoActual={keyEventInfoActual}");
            if (IsKeyBindRecording)
            {
                hotkeySequenceActual.Add(keyEventInfoActual);
                HotkeySequenceTextUpdate();
            }
            else
            {
                if (hotkeySequenceTest.Count > 0)
                {
                    var reset = true;
                    var hotkeySequenceTestNext = hotkeySequenceTest[0];
                    //Log(LogLevel.Verbose, $"RawInputCapture_OnKeyboard: hotkeySequenceTestNext={hotkeySequenceTestNext}");
                    if (keyEventInfoActual == hotkeySequenceTestNext)
                    {
                        hotkeySequenceTest.RemoveAt(0);
                        Log(LogLevel.Debug, $"RawInputCapture_OnKeyboard: MATCHED");
                        //Log(LogLevel.Verbose, $"RawInputCapture_OnKeyboard: Remaining: hotkeySequenceTest({hotkeySequenceTest.Count})={Utils.ToString(hotkeySequenceTest)}");
                        if (hotkeySequenceTest.Count == 0)
                        {
                            Log(LogLevel.Information, $"RawInputCapture_OnKeyboard: MATCHED WHOLE SEQUENCE!");
                            HotkeySequenceMatched?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            reset = false;
                        }
                    }
                    if (reset)
                    {
                        HotkeyListenerTestReset();
                    }
                }
                else
                {
                    //Log(LogLevel.Verbose, $"RawInputCapture_OnKeyboard: hotkeySequenceTest is empty; ignoring");
                }
            }
        }

        private void HotkeyListenerTestReset()
        {
            if (hotkeySequenceTest != hotkeySequenceActual)
            {
                hotkeySequenceTest.Clear();
                hotkeySequenceTest.AddRange(hotkeySequenceActual);
            }
        }

        private void buttonPredefinedKeyBindAdd_Click(object sender, EventArgs e)
        {
            if (IsKeyBindRecording)
            {
                // End recording hotkey sequence
                IsKeyBindRecording = false;
                hotkeySequencePrevious.Clear();
                hotkeySequenceTest.Clear();
                HotkeySequenceRecorded?.Invoke(this, EventArgs.Empty);
                HotkeyListenerTestReset();
            }
            else
            {
                // Start recording hotkey sequence
                hotkeySequencePrevious.Clear();
                hotkeySequencePrevious.AddRange(hotkeySequenceActual);
                hotkeySequenceActual.Clear();
                hotkeySequenceTest.Clear();
                IsKeyBindRecording = true;
            }

        }

        private void buttonPredefinedKeyBindRemove_Click(object sender, EventArgs e)
        {
            if (IsKeyBindRecording)
            {
                // Cancel recording hotkey sequence
                IsKeyBindRecording = false;
                hotkeySequenceActual.Clear();
                hotkeySequenceActual.AddRange(hotkeySequencePrevious);
                hotkeySequencePrevious.Clear();
                hotkeySequenceTest.Clear();
            }
            else
            {
                // Remove the saved hotkey sequence
                hotkeySequenceActual.Clear();
                hotkeySequencePrevious.Clear();
                hotkeySequenceTest.Clear();
                HotkeySequenceTextUpdate();
            }
        }
    }
}
