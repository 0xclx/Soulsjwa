using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Groups the test classes that redirect <see cref="Console.Out"/> to capture
/// log output, so they never run concurrently with each other.
///
/// <c>Console.Out</c> is process-global, and two classes swapping it at once can
/// interleave as: A saves the real writer, B saves A's writer, A restores the
/// real one, B restores A's — leaving every later <c>Console.Write</c> in the
/// process going to a dead <see cref="ConsoleCapture"/>. Only the save/restore
/// pairs corrupt each other; classes that merely emit output are harmless.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "ConsoleCapture";
}
