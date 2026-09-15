using PrismDemo.Core.Models;

namespace PrismDemo.Core.Interfaces
{
    public interface IAlarmConfigProvider
    {
        AlarmConfig GetConfig(string name);
    }
}
