namespace Daoris.Desktop;

/// <summary>What a press on the browser tells the person in a box: it failed to come up or answer; any other throw is a defect for the log.</summary>
public static class BringUpFailure
{
    public static bool Is(Exception error) =>
        error is InvalidOperationException or TimeoutException or HttpRequestException or System.Net.WebSockets.WebSocketException or TaskCanceledException;
}
