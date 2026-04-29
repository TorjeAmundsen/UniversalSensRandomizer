using System;
using System.IO;

namespace UniversalSensRandomizer.Services;

public sealed class FallbackRawAccelClient : IRawAccelClient
{
    private readonly IoctlRawAccelClient ioctl;

    public FallbackRawAccelClient(IoctlRawAccelClient ioctl)
    {
        this.ioctl = ioctl;
    }

    public byte[] Read()
    {
        return ioctl.Read();
    }

    public void Write(byte[] buffer)
    {
        try
        {
            ioctl.Write(buffer);
        }
        catch (IOException)
        {
            // Direct ioctl failed. writer.exe path requires a JSON settings file rather than the raw buffer,
            // and reconstructing valid settings.json from the binary buffer is non-trivial. Re-throw so the
            // caller can surface the error. A future enhancement can implement JSON reconstruction.
            throw;
        }
    }
}
