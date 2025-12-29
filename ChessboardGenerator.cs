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
        public int SquareSize { get; set; } = 80;   // 격자 크기 (Pixel)
        public int Margin { get; set; } = 100;      // 여백 (Pixel)
        public double DistortionStrength { get; set; } = 0.2; // 왜곡 강도 (0.0 ~ 0.5)

        public List<string> GenerateImages(string outputDir, int count)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            List<string> generatedFiles = new List<string>();

            // 1. 베이스 체스보드 그리기 (흰 배경)
            int boardW = SquaresX * SquareSize;
            int boardH = SquaresY * SquareSize;
            int imgW = boardW + Margin * 2;
            int imgH = boardH + Margin * 2;

            Size imgSize = new Size(imgW, imgH);

            using (Mat baseMat = new Mat(imgSize, MatType.CV_8UC1, Scalar.All(255)))
            {
                // 검은색 사각형 그리기
                for (int y = 0; y < SquaresY; y++)
                {
                    for (int x = 0; x < SquaresX; x++)
                    {
                        if ((x + y) % 2 == 1) // 체스 패턴 조건
                        {
                            int px = Margin + x * SquareSize;
                            int py = Margin + y * SquareSize;
                            Cv2.Rectangle(baseMat, new Rect(px, py, SquareSize, SquareSize), Scalar.All(0), -1);
                        }
                    }
                }

                Random rand = new Random();

                // 2. 랜덤 변환 적용하여 저장
                for (int i = 0; i < count; i++)
                {
                    // 원본 네 모서리 좌표
                    Point2f[] srcPts = new Point2f[]
                    {
                        new Point2f(0, 0),
                        new Point2f(imgW, 0),
                        new Point2f(imgW, imgH),
                        new Point2f(0, imgH)
                    };

                    // 왜곡 정도 계산 (이미지 크기 비례)
                    float maxDx = (float)(imgW * DistortionStrength);
                    float maxDy = (float)(imgH * DistortionStrength);

                    // 목표 네 모서리 좌표 (랜덤 오프셋 적용)
                    Point2f[] dstPts = new Point2f[]
                    {
                        new Point2f(GetRand(rand, maxDx), GetRand(rand, maxDy)),  // 좌상
                        new Point2f(imgW - GetRand(rand, maxDx), GetRand(rand, maxDy)), // 우상
                        new Point2f(imgW - GetRand(rand, maxDx), imgH - GetRand(rand, maxDy)), // 우하
                        new Point2f(GetRand(rand, maxDx), imgH - GetRand(rand, maxDy)) // 좌하
                    };

                    // 투시 변환 행렬 및 적용
                    using (Mat M = Cv2.GetPerspectiveTransform(srcPts, dstPts))
                    using (Mat warped = new Mat())
                    {
                        // 빈 공간은 흰색으로 채움
                        Cv2.WarpPerspective(baseMat, warped, M, imgSize, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));

                        string fileName = Path.Combine(outputDir, $"chess_gen_{i + 1:D3}.jpg");
                        warped.SaveImage(fileName);
                        generatedFiles.Add(fileName);
                    }
                }
            }

            return generatedFiles;
        }

        private float GetRand(Random r, float max)
        {
            // 0 ~ max 사이의 랜덤 값 반환
            return (float)(r.NextDouble() * max);
        }
    }
}
