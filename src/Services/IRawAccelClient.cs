namespace UniversalSensRandomizer.Services;

public interface IRawAccelClient
{
    byte[] Read();
    void Write(byte[] buffer);
}
