using Microsoft.Win32;                  // 파일 열기 대화상자 등을 사용하기 위해 추가
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;        // OpenCV 이미지를 WPF에서 보여주기 위한 확장 기능
using System;
using System.Collections.ObjectModel;   // 화면에 즉시 반영되는 리스트를 위해 사용
using System.ComponentModel;            // 속성 변경 알림(INotifyPropertyChanged)을 위해 사용
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;           // 비동기 작업(백그라운드 처리)을 위해 사용
using System.Windows;
using System.Windows.Input;             // 명령(ICommand) 처리를 위해 사용
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChessboardGenApp
{
    // INotifyPropertyChanged: 값이 바뀌면 화면에 "나 바뀌었어!"라고 알려주는 기능을 약속합니다.
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
        // 생성된 파일 목록 (이 리스트에 추가하면 화면의 목록 상자에 바로 뜹니다)
        public ObservableCollection<string> FileList { get; set; } = new ObservableCollection<string>();

        // 목록에서 선택된 파일
        private string? _selectedFile;
        public string? SelectedFile
        {
            get => _selectedFile;
            set
            {
                _selectedFile = value;
                OnPropertyChanged();

                // 파일을 선택하면 이미지를 로드해서 보여줍니다.
                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                {
                    LoadImage(value);
                }
            }
        }

        // 화면에 보여줄 이미지 소스
        private ImageSource? _displayImage;
        public ImageSource? DisplayImage { get => _displayImage; set { _displayImage = value; OnPropertyChanged(); } }
        #endregion

        // 버튼 클릭 명령들
        public ICommand BrowseCommand { get; }
        public ICommand GenerateCommand { get; }

        // 생성자: ViewModel이 처음 만들어질 때 실행됨
        public MainViewModel()
        {
            BrowseCommand = new RelayCommand(ExecuteBrowse);
            GenerateCommand = new RelayCommand(ExecuteGenerate);
        }

        private void ExecuteBrowse(object? obj)
        {
            // 폴더 선택 다이얼로그 (OpenFileDialog를 이용한 편법 혹은 FolderBrowserDialog 사용)
            // (WPF에는 기본 폴더 선택기가 없어서 OpenFileDialog를 약간 변형해서 사용)
            var dialog = new OpenFileDialog();
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.FileName = "Folder Selection";   // 가짜 파일 이름 설정

            if (dialog.ShowDialog() == true)
            {
                // 선택한 경로의 폴더 부분만 가져와서 설정합니다.
                // 만약 null이 반환되면 빈 문자열("")을 넣어서 null 에러를 방지합니다.
                OutputPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }

        // "GENERATE" 버튼을 눌렀을 때 실행 (비동기 처리 async)
        private async void ExecuteGenerate(object? obj)
        {
            if (IsBusy) return;     // 이미 작업 중이면 무시
            IsBusy = true;
            StatusMessage = "Generating Images..."; // 상태 메시지 변경
            FileList.Clear();       // 기존 목록 비우기

            try
            {
                // Task.Run: 오래 걸리는 작업을 화면이 멈추지 않게 별도 스레드에서 실행
                await Task.Run(() =>
                {
                    // ChessboardGenerator 인스턴스 생성 및 설정값 전달
                    ChessboardGenerator gen = new ChessboardGenerator
                    {
                        SquaresX = this.SquaresX,
                        SquaresY = this.SquaresY,
                        SquareSize = this.SquareSize,
                        Margin = this.SquareSize, // 여백은 사각형 크기만큼 자동 설정
                        DistortionStrength = this.Distortion
                    };

                    // 이미지 생성 실행!
                    var files = gen.GenerateImages(OutputPath, GenCount);

                    // UI 업데이트는 반드시 메인 스레드(Dispatcher)에서 해야 함
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var f in files) FileList.Add(f);           // 목록에 파일 추가
                        if (FileList.Count > 0) SelectedFile = FileList[0]; // 첫 번째 파일 자동 선택
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

        // 이미지 파일을 화면에 표시하기 위해 로드하는 함수
        private void LoadImage(string path)
        {
            // 이미지를 그냥 로드하면 파일이 잠겨서(Lock) 삭제 등을 못하게 됩니다.
            // 이를 방지하기 위해 메모리로 복사해서 로드하는 방식입니다.
            // 파일 락 방지를 위해 메모리 로드
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // 로드 시점에 메모리에 캐시
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            DisplayImage = bmp; // 화면 바인딩 속성에 할당
        }


        // 속성 변경 알림 구현부 (MVVM 필수 요소)
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ICommand를 쉽게 쓰기 위한 헬퍼 클래스(버튼 클릭 이벤트를 함수로 연결)
    // 간단한 RelayCommand 구현
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;       // 언제나 실행 가능
        public void Execute(object? parameter) => _execute(parameter);   // 실행 시 전달된 함수 호출

        /*
         * RelayCommand 구현에서는 CanExecute가 항상 true를 반환하도록 되어 있어서, 
         * 사실상 CanExecuteChanged 이벤트가 발생할 일이 없습니다. 
         * 하지만 ICommand 인터페이스의 규칙을 지키기 위해 선언은 반드시 해야 하며, 
         * 요즘 C#(.NET 6 이상) 환경에서는 이렇게 ?를 붙여주는 것이 표준입니다.
         */
        public event EventHandler? CanExecuteChanged;
    }
}