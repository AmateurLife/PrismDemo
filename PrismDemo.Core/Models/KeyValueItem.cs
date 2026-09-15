using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Mvvm;

namespace PrismDemo.Core.Models
{
    /// <summary>
    /// 可通知的项类
    /// </summary>
    public class KeyValueItem :BindableBase
    {
        private string _name;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        private string _value;

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
