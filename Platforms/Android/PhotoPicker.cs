using Android.App;

namespace Hakufu.Platforms.Droid;

/// <summary>Provisional (Task 6 del plan de Android).</summary>
internal static class PhotoPicker
{
    public static Task<string?> PickAsync(Activity activity) => Task.FromResult<string?>(null);
}
