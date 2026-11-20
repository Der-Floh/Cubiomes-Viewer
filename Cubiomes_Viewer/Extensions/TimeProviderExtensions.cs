using System.Globalization;

namespace Cubiomes_Viewer.Extensions;

public static class TimeProviderExtensions
{
    private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";

    extension(TimeProvider timeProvider)
    {
        /// <summary>The local date and time to the second as file names carry it, such as 20261120-154512.</summary>
        public string GetFileNameTimestamp()
        {
            ArgumentNullException.ThrowIfNull(timeProvider);

            return timeProvider.GetLocalNow().ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);
        }
    }
}
