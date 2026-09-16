using System.Windows;
using System.Windows.Controls;

namespace Soulsjwa.Connector.Behaviors;

/// <summary>
/// Two-way binding for <see cref="PasswordBox.Password"/>, which WPF deliberately
/// leaves out of the dependency-property system so a secret is never parked in a
/// binding expression or the element's property store.
/// <para>
/// Usage (order matters — <c>Attach</c> must be set before the bound value, so the
/// <see cref="PasswordBox.PasswordChanged"/> handler is hooked up before the
/// binding pushes its first value):
/// </para>
/// <code>
/// &lt;PasswordBox behaviors:PasswordBoxBinding.Attach="True"
///              behaviors:PasswordBoxBinding.BoundPassword="{Binding ApiKey, Mode=TwoWay}" /&gt;
/// </code>
/// </summary>
public static class PasswordBoxBinding
{
    /// <summary>Hooks the password box up to <see cref="BoundPasswordProperty"/>.</summary>
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false, OnAttachChanged));

    /// <summary>The bindable mirror of <see cref="PasswordBox.Password"/>.</summary>
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    /// <summary>
    /// Set while this class is writing <see cref="BoundPasswordProperty"/> from the
    /// password box, so the resulting change notification does not write the value
    /// straight back into the box (which would reset the caret on every keystroke).
    /// </summary>
    private static readonly DependencyProperty IsSyncingProperty =
        DependencyProperty.RegisterAttached(
            "IsSyncing",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    public static bool GetAttach(DependencyObject element) =>
        (bool)element.GetValue(AttachProperty);

    public static void SetAttach(DependencyObject element, bool value) =>
        element.SetValue(AttachProperty, value);

    public static string GetBoundPassword(DependencyObject element) =>
        (string)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string value) =>
        element.SetValue(BoundPasswordProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;

        if (e.OldValue is true)
            passwordBox.PasswordChanged -= OnPasswordChanged;

        if (e.NewValue is true)
            passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;
        if ((bool)passwordBox.GetValue(IsSyncingProperty)) return;

        var newPassword = e.NewValue as string ?? string.Empty;
        if (!string.Equals(passwordBox.Password, newPassword, StringComparison.Ordinal))
            passwordBox.Password = newPassword;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var passwordBox = (PasswordBox)sender;

        passwordBox.SetValue(IsSyncingProperty, true);
        try
        {
            SetBoundPassword(passwordBox, passwordBox.Password);
        }
        finally
        {
            passwordBox.SetValue(IsSyncingProperty, false);
        }
    }
}
