using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class ReportDataViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;
        private bool _disposed;

        private static readonly Dictionary<string, string> TableDisplayNames = new()
        {
            ["ConnectData"] = "采集数据表",
            ["ConnectConfig"] = "采集数据配置表",
            ["AData"] = "A剂数据表",
            ["AConfig"] = "A剂数据配置",
            ["BData"] = "B剂数据表",
            ["BConfig"] = "B剂数据配置",
            
        };

        public class TableOption
        {
            public string TableName { get; set; }
            public string DisplayName { get; set; }
        }

        public class SelectableColumn : BindableBase
        {
            public string ColumnName { get; set; }
            private bool _isSelected = true;
            public bool IsSelected
            {
                get => _isSelected;
                set => SetProperty(ref _isSelected, value);
            }
        }

        public DelegateCommand ChangeHomePageCmd { get; }
        public DelegateCommand QueryCmd { get; }
        public DelegateCommand ExportCsvCmd { get; }
        public DelegateCommand RefreshCmd { get; }

        private ObservableCollection<TableOption> _tableOptions = new();
        public ObservableCollection<TableOption> TableOptions
        {
            get => _tableOptions;
            set => SetProperty(ref _tableOptions, value);
        }

        private TableOption _selectedTable;
        public TableOption SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    ReportTitle = value != null
                        ? $"报表 — {value.DisplayName}"
                        : "报表";
                    LoadColumnsAsync();
                }
            }
        }

        private string _reportTitle = "报表";
        public string ReportTitle
        {
            get => _reportTitle;
            set => SetProperty(ref _reportTitle, value);
        }

        private DateTime _startTime = DateTime.Now.AddHours(-24);
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private DateTime _endTime = DateTime.Now;
        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private ObservableCollection<SelectableColumn> _allColumns = new();
        public ObservableCollection<SelectableColumn> AllColumns
        {
            get => _allColumns;
            set => SetProperty(ref _allColumns, value);
        }

        private DataTable _reportData;
        public DataTable ReportData
        {
            get => _reportData;
            set => SetProperty(ref _reportData, value);
        }

        public ReportDataViewModel(IRegionManager regionManager, IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService;

            ChangeHomePageCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "HomePage"));
            QueryCmd = new DelegateCommand(async () => await QueryAsync());
            ExportCsvCmd = new DelegateCommand(ExportCsv);
            RefreshCmd = new DelegateCommand(async () => await LoadTableNamesAsync());

            LoadTableNamesAsync();
        }

        private async Task LoadTableNamesAsync()
        {
            try
            {
                var names = await _databaseService.GetTableNamesAsync();
                var options = names.Select(n => new TableOption
                {
                    TableName = n,
                    DisplayName = TableDisplayNames.TryGetValue(n, out var d) ? d : n
                }).ToList();
                TableOptions = new ObservableCollection<TableOption>(options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ReportData] 加载表名失败: " + ex.Message);
            }
        }

        private async void LoadColumnsAsync()
        {
            if (SelectedTable == null) return;
            try
            {
                var columns = await GetColumnsForTableAsync(SelectedTable.TableName);
                AllColumns = new ObservableCollection<SelectableColumn>(
                    columns.Select(c => new SelectableColumn { ColumnName = c }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ReportData] 加载列名失败: " + ex.Message);
            }
        }

        private async Task<List<string>> GetColumnsForTableAsync(string tableName)
        {
            var rows = await _databaseService.QueryDataAsync(tableName, null, null, null);
            return rows.Count > 0
                ? rows[0].Keys.ToList()
                : new List<string>();
        }

        private async Task QueryAsync()
        {
            if (SelectedTable == null)
            {
                ShowMessageBox("请先选择数据表");
                return;
            }

            var selectedCols = AllColumns.Where(c => c.IsSelected).Select(c => c.ColumnName).ToList();
            if (selectedCols.Count == 0)
            {
                ShowMessageBox("请至少选择一个字段");
                return;
            }

            try
            {
                var start = StartTime.Date;
                var end = EndTime.Date.AddDays(1).AddSeconds(-1);

                var rows = await _databaseService.QueryDataAsync(
                    SelectedTable.TableName, selectedCols, start, end);

                var dt = new DataTable();
                foreach (var col in selectedCols)
                    dt.Columns.Add(col);

                foreach (var row in rows)
                {
                    var dr = dt.NewRow();
                    foreach (var col in selectedCols)
                    {
                        var val = row.TryGetValue(col, out var v) ? v : DBNull.Value;
                        dr[col] = val is DBNull ? DBNull.Value : val;
                    }
                    dt.Rows.Add(dr);
                }

                _reportData?.Dispose();
                ReportData = dt;
                WriteLog($"[ReportData] 查询完成，{dt.Rows.Count} 行");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ReportData] 查询失败: " + ex.Message);
                ShowMessageBox("查询失败: " + ex.Message);
            }
        }

        private void ExportCsv()
        {
            if (ReportData == null || ReportData.Rows.Count == 0)
            {
                ShowMessageBox("没有数据可导出");
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                var cols = ReportData.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
                sb.AppendLine(string.Join(",", cols.Select(EscapeCsv)));

                foreach (DataRow row in ReportData.Rows)
                {
                    var values = cols.Select(c =>
                    {
                        var v = row[c];
                        return v is DBNull || v == null ? "" : EscapeCsv(v.ToString());
                    });
                    sb.AppendLine(string.Join(",", values));
                }

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                ShowMessageBox($"导出成功: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                ShowMessageBox("导出失败: " + ex.Message);
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public void OnNavigatedTo(NavigationContext navigationContext) { }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            WriteLog("[ReportData] 离开页面");
            _reportData?.Dispose();
            _reportData = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reportData?.Dispose();
            _reportData = null;

            WriteLog("[ReportData] Dispose 完成");
        }
    }
}
