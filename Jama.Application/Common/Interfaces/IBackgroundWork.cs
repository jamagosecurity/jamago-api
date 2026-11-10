namespace Jama.Application.Common.Interfaces;

/// <summary>
/// Work that should happen soon but that nobody has to wait for — such as the notification emails after a client answers
/// their bill, which can take several seconds to send. The caller returns at once; the work runs on a background worker.
///
/// The work must not touch anything tied to the request (the database context, the signed-in user): hand it plain values and
/// singleton services only.
/// </summary>
public interface IBackgroundWork
{
    void Enqueue(Func<CancellationToken, Task> work);
}
