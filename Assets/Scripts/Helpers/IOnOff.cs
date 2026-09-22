public interface IOnOff
{
    bool IsOn { get; }
    void SetOn(bool on);
}