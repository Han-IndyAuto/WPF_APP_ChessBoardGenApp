using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace ChessboardGenApp
{
    public class ChessboardGenerator
    {
        public int SquaresX { get; set; } = 10;     // 가로 격자 수
        public int SquaresY { get; set; } = 7;      // 세로 격자 수
        public int SquareSize { get; set; } = 80;   // 격자 하나당 크기 (픽셀 단위)
        public int Margin { get; set; } = 100;      // 체스보드 주변의 여백 크기
        public double DistortionStrength { get; set; } = 0.2; // 이미지를 얼마나 기울일지 강도 (0.0은 평면, 0.5는 심한 기울기)

        // ---------------------------------------------------------
        // 이미지를 생성하는 핵심 함수
        // outputDir: 저장할 폴더 경로, count: 만들 이미지 개수
        // ---------------------------------------------------------
        public List<string> GenerateImages(string outputDir, int count)
        {
            // 저장할 폴더가 없으면 새로 생성.
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 생성된 파일 경로들을 담아둘 리스트를 만듭니다.
            List<string> generatedFiles = new List<string>();

            // 1. 베이스 체스보드 그리기 (흰 배경)
            // 기본 체스보드 크기 계산
            int boardW = SquaresX * SquareSize; // 전체 보드 가로 길이
            int boardH = SquaresY * SquareSize; // 전체 보드 세로 길이
            int imgW = boardW + Margin * 2;     // 여백을 포함한 전체 이미지 가로 길이
            int imgH = boardH + Margin * 2;     // 여백을 포함한 전체 이미지 세로 길이

            Size imgSize = new Size(imgW, imgH); // 이미지 크기 객체 생성

            // 하얀색 배경의 기본 이미지(Mat) 생성. (255는 흰색을 의미)
            using (Mat baseMat = new Mat(imgSize, MatType.CV_8UC1, Scalar.All(255)))
            {
                // 검은색 사각형 그리기
                // 격자 그리기 반복문: 세로(y)와 가로(x) 방향으로 반복
                for (int y = 0; y < SquaresY; y++)
                {
                    for (int x = 0; x < SquaresX; x++)
                    {
                        if ((x + y) % 2 == 1) // 체스 패턴 조건: x와 y 좌표 합이 홀수일 때만 검은색을 칠합니다.
                        {
                            // 사각형이 그려질 픽셀 좌표 계산
                            int px = Margin + x * SquareSize;
                            int py = Margin + y * SquareSize;

                            // 검은색(Scalar.All(0)) 사각형을 그립니다. -1은 내부를 채운다
                            Cv2.Rectangle(baseMat, new Rect(px, py, SquareSize, SquareSize), Scalar.All(0), -1);
                        }
                    }
                }

                // 무작위 값을 만들기 위한 도구
                Random rand = new Random();

                // 2. 랜덤 변환 적용하여 저장
                // 요청한 개수(count)만큼 이미지를 변형해서 저장
                for (int i = 0; i < count; i++)
                {
                    // 원본 네 모서리 좌표
                    // [변환 전] 원본 이미지의 네 모서리 좌표 (좌상, 우상, 우하, 좌하)
                    Point2f[] srcPts = new Point2f[]
                    {
                        new Point2f(0, 0),
                        new Point2f(imgW, 0),
                        new Point2f(imgW, imgH),
                        new Point2f(0, imgH)
                    };

                    // 왜곡 정도 계산 (이미지 크기 비례): 왜곡(기울기)의 최대 범위를 계산
                    float maxDx = (float)(imgW * DistortionStrength);
                    float maxDy = (float)(imgH * DistortionStrength);

                    // [변환 후] 네 모서리가 이동할 좌표를 랜덤하게 정합니다.
                    // 이렇게 하면 이미지가 3차원 공간에서 기울어진 것처럼 보입니다.
                    Point2f[] dstPts = new Point2f[]
                    {
                        new Point2f(GetRand(rand, maxDx), GetRand(rand, maxDy)),  // 좌상
                        new Point2f(imgW - GetRand(rand, maxDx), GetRand(rand, maxDy)), // 우상
                        new Point2f(imgW - GetRand(rand, maxDx), imgH - GetRand(rand, maxDy)), // 우하
                        new Point2f(GetRand(rand, maxDx), imgH - GetRand(rand, maxDy)) // 좌하
                    };

                    // 투시 변환 행렬(M)을 계산합니다. (어떻게 찌그러뜨릴지 계산)
                    using (Mat M = Cv2.GetPerspectiveTransform(srcPts, dstPts))
                    using (Mat warped = new Mat())  // 결과가 담길 빈 이미지
                    {
                        // 원본(baseMat)을 M 행렬에 따라 변환(WarpPerspective)하여 warped에 담습니다.
                        // 빈 공간(Border)은 흰색(Scalar.All(255))으로 채웁니다.
                        Cv2.WarpPerspective(baseMat, warped, M, imgSize, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));

                        // 파일 이름을 만듭니다. (예: chess_gen_001.jpg)
                        string fileName = Path.Combine(outputDir, $"chess_gen_{i + 1:D3}.jpg");
                        // 이미지를 파일로 저장합니다.
                        warped.SaveImage(fileName);
                        // 저장된 파일 경로를 목록에 추가합니다.
                        generatedFiles.Add(fileName);
                    }
                }
            }

            return generatedFiles;  // 생성된 파일 목록 반환
        }

        // 0부터 max 사이의 랜덤한 실수값을 주는 도우미 함수
        private float GetRand(Random r, float max)
        {
            // 0 ~ max 사이의 랜덤 값 반환
            return (float)(r.NextDouble() * max);
        }
    }
}
