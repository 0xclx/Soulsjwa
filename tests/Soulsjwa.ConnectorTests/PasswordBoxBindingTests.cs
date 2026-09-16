using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Data;
using FluentAssertions;
using Soulsjwa.Connector.Behaviors;

namespace Soulsjwa.ConnectorTests;

// Covers the attached behaviour behind the masked API key field in
// MainWindow.xaml: PasswordBox.Password is not a dependency property, so the
// binding to MainViewModel.ApiKey goes through PasswordBoxBinding instead.
// Constructing a WPF control needs an STA thread, hence RunOnStaThread — like
// the rest of this project these tests only execute on the windows-latest job.
public class PasswordBoxBindingTests
{
    /// <summary>Stand-in for the view model's ApiKey property.</summary>
    private sealed class SecretHolder : INotifyPropertyChanged
    {
        private string _secret = string.Empty;

        public string Secret
        {
            get => _secret;
            set
            {
                if (string.Equals(value, _secret, StringComparison.Ordinal)) return;
                _secret = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Secret)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static void RunOnStaThread(Action test)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }

    private static PasswordBox CreateBoundPasswordBox(SecretHolder source)
    {
        var passwordBox = new PasswordBox();

        // Same order as the XAML: Attach hooks PasswordChanged up before the
        // binding pushes its first value in.
        PasswordBoxBinding.SetAttach(passwordBox, true);
        passwordBox.DataContext = source;
        BindingOperations.SetBinding(
            passwordBox,
            PasswordBoxBinding.BoundPasswordProperty,
            new Binding(nameof(SecretHolder.Secret))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });

        return passwordBox;
    }

    [Fact]
    public void SourceValue_FlowsIntoThePasswordBox() => RunOnStaThread(() =>
    {
        // A key loaded from connector-config.json is already set when the
        // window binds.
        var source = new SecretHolder { Secret = "sk_loaded_from_config" };

        var passwordBox = CreateBoundPasswordBox(source);

        passwordBox.Password.Should().Be("sk_loaded_from_config");

        source.Secret = "sk_changed_on_the_view_model";

        passwordBox.Password.Should().Be("sk_changed_on_the_view_model");
    });

    [Fact]
    public void TypedPassword_FlowsBackToTheSource() => RunOnStaThread(() =>
    {
        var source = new SecretHolder();
        var passwordBox = CreateBoundPasswordBox(source);

        passwordBox.Password = "sk_typed_by_the_user";

        source.Secret.Should().Be("sk_typed_by_the_user");
    });

    [Fact]
    public void ClearedPassword_ClearsTheSource() => RunOnStaThread(() =>
    {
        var source = new SecretHolder { Secret = "sk_loaded_from_config" };
        var passwordBox = CreateBoundPasswordBox(source);

        passwordBox.Password = string.Empty;

        source.Secret.Should().BeEmpty();
    });

    [Fact]
    public void DetachedPasswordBox_StopsReportingChanges() => RunOnStaThread(() =>
    {
        var source = new SecretHolder();
        var passwordBox = CreateBoundPasswordBox(source);

        PasswordBoxBinding.SetAttach(passwordBox, false);
        passwordBox.Password = "sk_typed_after_detaching";

        source.Secret.Should().BeEmpty();
    });
}
