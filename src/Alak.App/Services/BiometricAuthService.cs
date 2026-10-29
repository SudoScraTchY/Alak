using System;
using System.Threading.Tasks;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Runtime;
#endif

namespace Alak.App.Services;

public class BiometricAuthService : IBiometricAuthService
{
    public bool IsAvailable()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var keyguardManager = (KeyguardManager?)context.GetSystemService(Context.KeyguardService);
            if (keyguardManager == null) return false;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                return keyguardManager.IsDeviceSecure;
            }
            return keyguardManager.IsKeyguardSecure;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public async Task<bool> AuthenticateAsync(string title, string subtitle, string cancelText = "Cancel")
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null) return false;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                var tcs = new TaskCompletionSource<bool>();
                var cancellationSignal = new CancellationSignal();
                var callback = new AndroidBiometricCallback(tcs);

                var builder = new BiometricPrompt.Builder(activity)
                    .SetTitle(title)
                    .SetSubtitle(subtitle);

                builder.SetNegativeButton(cancelText, activity.MainExecutor, new DialogInterfaceOnClickListener((_, _) =>
                {
                    try { cancellationSignal.Cancel(); } catch { }
                    tcs.TrySetResult(false);
                }));

                var prompt = builder.Build();
                prompt.Authenticate(cancellationSignal, activity.MainExecutor, callback);

                return await tcs.Task;
            }
            else
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("AlakBiometric", $"Biometric authentication error: {ex.Message}");
            return false;
        }
#else
        await Task.CompletedTask;
        return true;
#endif
    }

#if ANDROID
    private class DialogInterfaceOnClickListener : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        private readonly Action<IDialogInterface?, int> _action;

        public DialogInterfaceOnClickListener(Action<IDialogInterface?, int> action)
        {
            _action = action;
        }

        public void OnClick(IDialogInterface? dialog, int which)
        {
            _action(dialog, which);
        }
    }

    private class AndroidBiometricCallback : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public AndroidBiometricCallback(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
        {
            base.OnAuthenticationSucceeded(result);
            _tcs.TrySetResult(true);
        }

        public override void OnAuthenticationFailed()
        {
            base.OnAuthenticationFailed();
        }

        public override void OnAuthenticationError([GeneratedEnum] BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
        {
            base.OnAuthenticationError(errorCode, errString);
            _tcs.TrySetResult(false);
        }
    }
#endif
}
