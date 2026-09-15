using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class SettingConnectViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;

        private string _selectedTable = "ConnectConfig";
        private string _searchText = string.Empty;
        private string _selectedTypeFilter = "全部";

        private List<ConnectData> _connectAllData;
        private ObservableCollection<ConnectData> _connectFilteredData;
        private readonly HashSet<string> _connectOriginalNames = new();

        private List<FilterConfigItem> _aAllData;
        private ObservableCollection<FilterConfigItem> _aFilteredData;
        private readonly HashSet<string> _aOriginalNames = new();

        private List<FilterConfigItem> _bAllData;
        private ObservableCollection<FilterConfigItem> _bFilteredData;
        private readonly HashSet<string> _bOriginalNames = new();

        public ObservableCollection<string> TableOptions { get; } = new() { "ConnectConfig", "AConfig", "BConfig" };

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    OnTableChanged();
                    RaisePropertyChanged(nameof(IsConnectMode));
                    RaisePropertyChanged(nameof(IsAMode));
                    RaisePropertyChanged(nameof(IsBMode));
                    RaisePropertyChanged(nameof(IsTypeFilterVisible));
                }
            }
        }

        public bool IsConnectMode => _selectedTable == "ConnectConfig";
        public bool IsAMode => _selectedTable == "AConfig";
        public bool IsBMode => _selectedTable == "BConfig";
        public bool IsTypeFilterVisible => IsConnectMode;

        public ObservableCollection<ConnectData> ConnectFilteredData
        {
            get => _connectFilteredData;
            set => SetProperty(ref _connectFilteredData, value);
        }

        public ObservableCollection<FilterConfigItem> AFilteredData
        {
            get => _aFilteredData;
            set => SetProperty(ref _aFilteredData, value);
        }

        public ObservableCollection<FilterConfigItem> BFilteredData
        {
            get => _bFilteredData;
            set => SetProperty(ref _bFilteredData, value);
        }

        public ObservableCollection<string> TypeFilters { get; } = new() { "全部" };

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                    ApplyFilter();
            }
        }

        public DelegateCommand ChangeSettingCmd { get; }
        public DelegateCommand<object> SaveItemCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddNewCommand { get; }
        public DelegateCommand<object> DeleteItemCommand { get; }

        public SettingConnectViewModel(IRegionManager regionManager,
            IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));

            ChangeSettingCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "Setting"));

            SaveItemCommand = new DelegateCommand<object>(async (item) => await SaveItemAsync(item));
            RefreshCommand = new DelegateCommand(async () => await LoadDataAsync());
            AddNewCommand = new DelegateCommand(AddNewItem);
            DeleteItemCommand = new DelegateCommand<object>(async (item) => await DeleteItemAsync(item));

            InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var tasks = new Task[]
            {
                LoadConnectDataAsync(),
                LoadFilterDataAsync("AConfig"),
                LoadFilterDataAsync("BConfig")
            };
            await Task.WhenAll(tasks);
            ApplyFilter();
        }

        private async Task LoadConnectDataAsync()
        {
            _connectAllData = await _databaseService.GetConnectData();

            foreach (var item in _connectAllData)
            {
                const string prefix = "ns=2;s=";
                if (item.OpcAddress != null && item.OpcAddress.StartsWith(prefix))
                    item.OpcAddress = item.OpcAddress.Substring(prefix.Length);
                if (item.MockOpcAddress != null && item.MockOpcAddress.StartsWith(prefix))
                    item.MockOpcAddress = item.MockOpcAddress.Substring(prefix.Length);
            }

            _connectOriginalNames.Clear();
            foreach (var item in _connectAllData)
            {
                if (!string.IsNullOrEmpty(item.Name))
                    _connectOriginalNames.Add(item.Name);
            }

            var types = _connectAllData.Select(x => x.Type).Distinct().OrderBy(x => x).ToList();
            TypeFilters.Clear();
            TypeFilters.Add("全部");
            foreach (var t in types)
            {
                if (!string.IsNullOrEmpty(t))
                    TypeFilters.Add(t);
            }
        }

        private async Task LoadFilterDataAsync(string tableName)
        {
            var data = await _databaseService.GetFilterConfigsAsync(tableName);
            var names = new HashSet<string>();
            foreach (var item in data)
            {
                if (!string.IsNullOrEmpty(item.Name))
                    names.Add(item.Name);
            }

            if (tableName == "AConfig")
            {
                _aAllData = data;
                _aOriginalNames.Clear();
                foreach (var n in names) _aOriginalNames.Add(n);
            }
            else
            {
                _bAllData = data;
                _bOriginalNames.Clear();
                foreach (var n in names) _bOriginalNames.Add(n);
            }
        }

        private void OnTableChanged()
        {
            ApplyFilter();
        }

        private void AddNewItem()
        {
            if (IsConnectMode)
            {
                _connectAllData.Add(new ConnectData());
                ApplyFilter();
            }
            else
            {
                var list = _selectedTable == "AConfig" ? _aAllData : _bAllData;
                list.Add(new FilterConfigItem());
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            if (IsConnectMode)
                ApplyConnectFilter();
            else if (_selectedTable == "AConfig")
                ApplyFilterFilter(ref _aFilteredData, _aAllData, nameof(AFilteredData));
            else
                ApplyFilterFilter(ref _bFilteredData, _bAllData, nameof(BFilteredData));
        }

        private void ApplyConnectFilter()
        {
            if (_connectAllData == null) return;

            var query = _connectAllData.AsEnumerable();

            if (!string.IsNullOrEmpty(_selectedTypeFilter) && _selectedTypeFilter != "全部")
            {
                query = query.Where(x => x.Type == _selectedTypeFilter);
            }

            if (!string.IsNullOrEmpty(_searchText))
            {
                var search = _searchText.Trim().ToLower();
                query = query.Where(x =>
                    (x.Name?.ToLower().Contains(search) ?? false) ||
                    (x.OpcAddress?.ToLower().Contains(search) ?? false) ||
                    (x.MockOpcAddress?.ToLower().Contains(search) ?? false) ||
                    (x.Description?.ToLower().Contains(search) ?? false));
            }

            ConnectFilteredData = new ObservableCollection<ConnectData>(query);
        }

        private void ApplyFilterFilter(ref ObservableCollection<FilterConfigItem> filtered,
            List<FilterConfigItem> allData, string propName)
        {
            if (allData == null) return;

            var query = allData.AsEnumerable();

            if (!string.IsNullOrEmpty(_searchText))
            {
                var search = _searchText.Trim().ToLower();
                query = query.Where(x =>
                    (x.Name?.ToLower().Contains(search) ?? false) ||
                    (x.ConnectName?.ToLower().Contains(search) ?? false));
            }

            filtered = new ObservableCollection<FilterConfigItem>(query);
            RaisePropertyChanged(propName);
        }

        private async Task SaveItemAsync(object param)
        {
            if (param == null) return;

            if (IsConnectMode && param is ConnectData connectItem)
                await SaveConnectItemAsync(connectItem);
            else if (param is FilterConfigItem filterItem)
                await SaveFilterItemAsync(filterItem);
        }

        private async Task SaveConnectItemAsync(ConnectData item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    ShowMessageBox("名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_connectOriginalNames.Contains(item.Name))
                    await _databaseService.UpdateConnectDataAsync(item);
                else
                {
                    await _databaseService.InsertConnectDataAsync(item);
                    _connectOriginalNames.Add(item.Name);
                }

                ShowMessageBox("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowMessageBox("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveFilterItemAsync(FilterConfigItem item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    ShowMessageBox("名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var table = _selectedTable;
                var originalNames = table == "AConfig" ? _aOriginalNames : _bOriginalNames;

                if (originalNames.Contains(item.Name))
                    await _databaseService.UpdateFilterConfigAsync(table, item);
                else
                {
                    await _databaseService.InsertFilterConfigAsync(table, item);
                    originalNames.Add(item.Name);
                }

                ShowMessageBox("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowMessageBox("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteItemAsync(object param)
        {
            if (param == null) return;

            if (IsConnectMode && param is ConnectData connectItem)
                await DeleteConnectItemAsync(connectItem);
            else if (param is FilterConfigItem filterItem)
                await DeleteFilterItemAsync(filterItem);
        }

        private async Task DeleteConnectItemAsync(ConnectData item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                _connectAllData.Remove(item);
                ApplyFilter();
                return;
            }

            var result = ShowMessageBox($"确定删除 \"{item.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _databaseService.DeleteConnectDataAsync(item.Name);
                _connectAllData.Remove(item);
                _connectOriginalNames.Remove(item.Name);
                ApplyFilter();
                ShowMessageBox("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowMessageBox("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteFilterItemAsync(FilterConfigItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                RemoveFilterItem(item);
                return;
            }

            var result = ShowMessageBox($"确定删除 \"{item.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var table = _selectedTable;
                var originalNames = table == "AConfig" ? _aOriginalNames : _bOriginalNames;

                await _databaseService.DeleteFilterConfigAsync(table, item.Name);
                RemoveFilterItem(item);
                originalNames.Remove(item.Name);
                ApplyFilter();
                ShowMessageBox("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowMessageBox("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFilterItem(FilterConfigItem item)
        {
            if (_selectedTable == "AConfig")
                _aAllData?.Remove(item);
            else
                _bAllData?.Remove(item);
            ApplyFilter();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
