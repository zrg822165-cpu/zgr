using System.Collections.Concurrent;
using System.Windows.Forms;

namespace OpenClaw.Tests.Integration;

public sealed class TestFixtureHost : IDisposable
{
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly string _windowTitle;
    private Exception? _startupException;
    private Thread? _uiThread;
    private FixtureForm? _form;

    public TestFixtureHost(string? windowTitle = null)
    {
        _windowTitle = windowTitle ?? $"OpenClaw Fixture {Guid.NewGuid():N}";
    }

    public string WindowTitle => _windowTitle;

    public string GetProxyAddressValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<string>(() => _form.ProxyAddressValue)) as string
            ?? throw new InvalidOperationException("Failed to read proxy address value from fixture window.");
    }

    public string GetStatusValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<string>(() => _form.StatusValue)) as string
            ?? throw new InvalidOperationException("Failed to read status value from fixture window.");
    }

    public bool GetAutoDetectEnabled()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<bool>(() => _form.AutoDetectEnabled)) is bool value
            ? value
            : throw new InvalidOperationException("Failed to read auto-detect checkbox value from fixture window.");
    }

    public string GetUsernameValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<string>(() => _form.UsernameValue)) as string
            ?? throw new InvalidOperationException("Failed to read username value from fixture window.");
    }

    public string GetPasswordValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<string>(() => _form.PasswordValue)) as string
            ?? throw new InvalidOperationException("Failed to read password value from fixture window.");
    }

    public bool GetOptionsExpanded()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<bool>(() => _form.OptionsExpanded)) is bool value
            ? value
            : throw new InvalidOperationException("Failed to read options dropdown state from fixture window.");
    }

    public int GetVolumeValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<int>(() => _form.VolumeValue)) is int value
            ? value
            : throw new InvalidOperationException("Failed to read volume control value from fixture window.");
    }

    public string GetSelectedOptionValue()
    {
        if (_form is null || _form.IsDisposed)
        {
            throw new InvalidOperationException("Fixture window is not running.");
        }

        return _form.Invoke(new Func<string>(() => _form.SelectedOptionValue)) as string
            ?? throw new InvalidOperationException("Failed to read selected option value from fixture window.");
    }

    public void Start()
    {
        if (_uiThread is not null)
        {
            return;
        }

        _uiThread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "OpenClawFixtureUI"
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Timed out waiting for the fixture window to start.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Fixture window failed to start.", _startupException);
        }
    }

    public void Dispose()
    {
        if (_form is not null && !_form.IsDisposed)
        {
            _form.Invoke(new Action(() => _form.Close()));
        }

        if (_uiThread is not null && !_uiThread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Timed out waiting for the fixture window to close.");
        }

        _ready.Dispose();
    }

    private void ThreadMain()
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _form = new FixtureForm(_windowTitle);
            _form.Shown += (_, _) => _ready.Set();
            Application.Run(_form);
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _ready.Set();
        }
    }

    private sealed class FixtureForm : Form
    {
        private readonly TextBox _input;
        private readonly TextBox _username;
        private readonly TextBox _password;
        private readonly Label _status;
        private readonly CheckBox _autoDetect;
        private readonly ComboBox _options;
        private readonly NumericUpDown _volume;

        public FixtureForm(string windowTitle)
        {
            Text = windowTitle;
            Name = "SettingsWindow";
            Width = 420;
            Height = 220;
            StartPosition = FormStartPosition.CenterScreen;

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(12),
                WrapContents = false,
            };

            var label = new Label
            {
                Name = "ProxyAddressLabel",
                Text = "代理地址",
                Width = 320,
                AutoSize = true,
            };

            _input = new TextBox
            {
                Name = "ProxyAddressInput",
                Text = "127.0.0.1:7890",
                Width = 320,
                TabIndex = 0,
            };

            var usernameLabel = new Label
            {
                Name = "UsernameLabel",
                Text = "用户名",
                Width = 320,
                AutoSize = true,
            };

            _username = new TextBox
            {
                Name = "UsernameInput",
                Text = string.Empty,
                Width = 320,
                TabIndex = 1,
            };

            var passwordLabel = new Label
            {
                Name = "PasswordLabel",
                Text = "密码",
                Width = 320,
                AutoSize = true,
            };

            _password = new TextBox
            {
                Name = "PasswordInput",
                Text = string.Empty,
                Width = 320,
                UseSystemPasswordChar = true,
                TabIndex = 2,
            };

            _autoDetect = new CheckBox
            {
                Name = "AutoDetectCheckbox",
                Text = "自动检测设置",
                Checked = false,
                Width = 320,
                TabIndex = 3,
            };

            _options = new ComboBox
            {
                Name = "AdvancedOptionsCombo",
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList,
                TabIndex = 4,
            };
            _options.Items.AddRange(["标准模式", "高级模式", "实验模式"]);
            _options.SelectedIndex = 0;

            var volumeLabel = new Label
            {
                Name = "VolumeLabel",
                Text = "音量",
                Width = 320,
                AutoSize = true,
            };

            _volume = new NumericUpDown
            {
                Name = "VolumeSlider",
                Width = 320,
                Minimum = 0,
                Maximum = 100,
                Value = 40,
                DecimalPlaces = 0,
                Increment = 1,
                TabIndex = 5,
            };

            var saveButton = new Button
            {
                Name = "SaveButton",
                Text = "保存",
                Width = 100,
                TabIndex = 6,
            };

            var loginButton = new Button
            {
                Name = "LoginButton",
                Text = "登录",
                Width = 100,
                TabIndex = 7,
            };

            var cancelButton = new Button
            {
                Name = "CancelButton",
                Text = "保存",
                Width = 100,
                TabIndex = 8,
            };

            _status = new Label
            {
                Name = "StatusLabel",
                Text = "未保存",
                Width = 320,
                AutoSize = true,
            };

            saveButton.Click += (_, _) => _status.Text = $"已保存:{_input.Text}";
            loginButton.Click += (_, _) => _status.Text = $"已登录:{_username.Text}/{_password.Text}";
            cancelButton.Click += (_, _) => _status.Text = $"次要保存:{_input.Text}";

            panel.Controls.Add(label);
            panel.Controls.Add(_input);
            panel.Controls.Add(usernameLabel);
            panel.Controls.Add(_username);
            panel.Controls.Add(passwordLabel);
            panel.Controls.Add(_password);
            panel.Controls.Add(_autoDetect);
            panel.Controls.Add(_options);
            panel.Controls.Add(volumeLabel);
            panel.Controls.Add(_volume);
            panel.Controls.Add(saveButton);
            panel.Controls.Add(loginButton);
            panel.Controls.Add(cancelButton);
            panel.Controls.Add(_status);

            Controls.Add(panel);
            Shown += (_, _) => _input.Focus();
        }

        public string ProxyAddressValue => _input.Text;

        public string UsernameValue => _username.Text;

        public string PasswordValue => _password.Text;

        public string StatusValue => _status.Text;

        public bool AutoDetectEnabled => _autoDetect.Checked;

        public bool OptionsExpanded => _options.DroppedDown;

        public string SelectedOptionValue => _options.SelectedItem?.ToString() ?? string.Empty;

        public int VolumeValue => decimal.ToInt32(_volume.Value);
    }
}
