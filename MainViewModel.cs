using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChessboardGenApp
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Properties
        // 보드 설정
        private int _squaresX = 10;
        public int SquaresX { get => _squaresX; set { _squaresX = value; OnPropertyChanged(); } }

        private int _squaresY = 7;
        public int SquaresY { get => _squaresY; set { _squaresY = value; OnPropertyChanged(); } }

        private int _squareSize = 80;
        public int SquareSize { get => _squareSize; set { _squareSize = value; OnPropertyChanged(); } }

        // 생성 설정
        private int _genCount = 20;
        public int GenCount { get => _genCount; set { _genCount = value; OnPropertyChanged(); } }

        private double _distortion = 0.2; // 0.0 ~ 0.5
        public double Distortion { get => _distortion; set { _distortion = value; OnPropertyChanged(); } }

        private string _outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ChessImages");
        public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); } }

        // 상태 및 결과
        private string _statusMessage = "Ready";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private bool _isBusy = false;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        // 생성된 파일 목록 및 뷰어
        public ObservableCollection<string> FileList { get; set; } = new ObservableCollection<string>();

        private string _selectedFile;
        public string SelectedFile
        {
            get => _selectedFile;
            set
            {
                _selectedFile = value;
                OnPropertyChanged();
                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                {
                    LoadImage(value);
                }
            }
        }

        private ImageSource _displayImage;
        public ImageSource DisplayImage { get => _displayImage; set { _displayImage = value; OnPropertyChanged(); } }
        #endregion

        public ICommand BrowseCommand { get; }
        public ICommand GenerateCommand { get; }

        public MainViewModel()
        {
            BrowseCommand = new RelayCommand(ExecuteBrowse);
            GenerateCommand = new RelayCommand(ExecuteGenerate);
        }

        private void ExecuteBrowse(object obj)
        {
            // 폴더 선택 다이얼로그 (OpenFileDialog를 이용한 편법 혹은 FolderBrowserDialog 사용)
            var dialog = new OpenFileDialog();
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.FileName = "Folder Selection";

            if (dialog.ShowDialog() == true)
            {
                OutputPath = Path.GetDirectoryName(dialog.FileName);
            }
        }

        private async void ExecuteGenerate(object obj)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Generating Images...";
            FileList.Clear();

            try
            {
                await Task.Run(() =>
                {
                    ChessboardGenerator gen = new ChessboardGenerator
                    {
                        SquaresX = this.SquaresX,
                        SquaresY = this.SquaresY,
                        SquareSize = this.SquareSize,
                        Margin = this.SquareSize, // 여백은 사각형 크기만큼 자동 설정
                        DistortionStrength = this.Distortion
                    };

                    var files = gen.GenerateImages(OutputPath, GenCount);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var f in files) FileList.Add(f);
                        if (FileList.Count > 0) SelectedFile = FileList[0];
                    });
                });

                StatusMessage = $"Completed. {FileList.Count} images generated.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
                MessageBox.Show(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadImage(string path)
        {
            // 파일 락 방지를 위해 메모리 로드
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            DisplayImage = bmp;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // 간단한 RelayCommand 구현
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged;
    }
}