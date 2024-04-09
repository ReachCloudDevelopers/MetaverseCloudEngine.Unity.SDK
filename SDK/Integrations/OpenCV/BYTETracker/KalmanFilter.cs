using OpenCVForUnity.CoreModule;

namespace MetaverseCloudEngine.Unity.OpenCV.BYTETracker
{
    public class KalmanFilter
    {
        private Mat _mean;
        private Mat _covariance;
        private readonly Mat _motionMat;
        private readonly Mat _updateMat;

        private readonly float _stdWeightPosition;
        private readonly float _stdWeightVelocity;

        public KalmanFilter(float stdWeightPosition = 1f / 20, float stdWeightVelocity = 1f / 160)
        {
            _stdWeightPosition = stdWeightPosition;
            _stdWeightVelocity = stdWeightVelocity;

            _motionMat = new Mat(8, 8, CvType.CV_32F);
            float[] transitionMatrixArr = {
                        1, 0, 0, 0, 1, 0, 0, 0,
                        0, 1, 0, 0, 0, 1, 0, 0,
                        0, 0, 1, 0, 0, 0, 1, 0,
                        0, 0, 0, 1, 0, 0, 0, 1,
                        0, 0, 0, 0, 1, 0, 0, 0,
                        0, 0, 0, 0, 0, 1, 0, 0,
                        0, 0, 0, 0, 0, 0, 1, 0,
                        0, 0, 0, 0, 0, 0, 0, 1
                    };
            _motionMat.put(0, 0, transitionMatrixArr);

            _updateMat = new Mat(4, 8, CvType.CV_32F);
            float[] measurementMatrixArr = {
                        1, 0, 0, 0, 0, 0, 0, 0,
                        0, 1, 0, 0, 0, 0, 0, 0,
                        0, 0, 1, 0, 0, 0, 0, 0,
                        0, 0, 0, 1, 0, 0, 0, 0
                    };
            _updateMat.put(0, 0, measurementMatrixArr);
        }

        public void Initiate(IRectBase measurement)
        {
            var rectData = RectToXyAh(measurement);
            var rectDataArr = new float[4];
            rectData.get(0, 0, rectDataArr);
            _mean = new Mat(1, 8, CvType.CV_32F);
            _mean.put(0, 0, rectDataArr);
            _mean.put(0, 4, 0f, 0f, 0f, 0f);

            var std = new[] {
                2 * _stdWeightPosition * measurement.Height,
                2 * _stdWeightPosition * measurement.Height,
                1e-2f,
                2 * _stdWeightPosition * measurement.Height,
                10 * _stdWeightVelocity * measurement.Height,
                10 * _stdWeightVelocity * measurement.Height,
                1e-5f,
                10 * _stdWeightVelocity * measurement.Height
            };
            _covariance = new Mat(std.Length, std.Length, CvType.CV_32F, new Scalar(0));
            for (var i = 0; i < std.Length; i++)
            {
                _covariance.put(i, i, std[i] * std[i]);
            }
        }

        public IRectBase Predict(bool meanEightToZero)
        {
            if (meanEightToZero) _mean.put(0, 7, 0f);

            var std = new[] {
                _stdWeightPosition * (float)_mean.get(0, 3)[0],
                _stdWeightPosition * (float)_mean.get(0, 3)[0],
                1e-2f,
                _stdWeightPosition * (float)_mean.get(0, 3)[0],
                _stdWeightVelocity * (float)_mean.get(0, 3)[0],
                _stdWeightVelocity * (float)_mean.get(0, 3)[0],
                1e-5f,
                _stdWeightVelocity * (float)_mean.get(0, 3)[0]
            };
            
            var motionCov = new Mat(std.Length, std.Length, CvType.CV_32F, new Scalar(0));
            for (var i = 0; i < std.Length; i++)
                motionCov.put(i, i, std[i] * std[i]);

            Core.gemm(_motionMat, _mean, 1, new Mat(), 0, _mean, Core.GEMM_2_T);
            Core.transpose(_mean, _mean);
            Core.gemm(_covariance, _motionMat, 1, new Mat(), 0, _covariance, Core.GEMM_2_T);
            Core.gemm(_motionMat, _covariance, 1, motionCov, 1, _covariance);

            return XyAhToRect(_mean.submat(0, 1, 0, 4));
        }

        public IRectBase Update(IRectBase measurement)
        {
            var projectedMean = new Mat(1, 4, CvType.CV_32F);
            var projectedCov = new Mat(4, 4, CvType.CV_32F);
            Project(ref projectedMean, ref projectedCov);

            var b = new Mat();
            Core.gemm(_covariance, _updateMat, 1, new Mat(), 0, b, Core.GEMM_2_T);
            Core.transpose(b, b);

            var invertedProjectedCov = new Mat();
            Core.invert(projectedCov, invertedProjectedCov);
            var kalmanGain = new Mat();
            Core.gemm(invertedProjectedCov, b, 1, new Mat(), 0, kalmanGain);
            Core.transpose(kalmanGain, kalmanGain);

            var innovation = new Mat();
            Core.subtract(RectToXyAh(measurement), projectedMean, innovation);

            var tmp = new Mat();
            Core.gemm(innovation, kalmanGain, 1, new Mat(), 0, tmp, Core.GEMM_2_T);
            Core.add(_mean, tmp, _mean);

            Core.gemm(kalmanGain, projectedCov, 1, new Mat(), 0, tmp);
            Core.gemm(tmp, kalmanGain, 1, new Mat(), 0, tmp, Core.GEMM_2_T);
            Core.subtract(_covariance, tmp, _covariance);

            return XyAhToRect(_mean.submat(0, 1, 0, 4));
        }

        private void Project(ref Mat projectedMean, ref Mat projectedCovariance)
        {
            var std = new[] {
                _stdWeightPosition * (float)_mean.get(0, 3)[0],
                _stdWeightPosition * (float)_mean.get(0, 3)[0],
                1e-1f,
                _stdWeightPosition * (float)_mean.get(0, 3)[0]
            };

            Core.gemm(_updateMat, _mean, 1, new Mat(), 0, projectedMean, Core.GEMM_2_T);
            Core.transpose(projectedMean, projectedMean);
            var tmp = new Mat();
            Core.gemm(_covariance, _updateMat, 1, new Mat(), 0, tmp, Core.GEMM_2_T);
            Core.gemm(_updateMat, tmp, 1, new Mat(), 0, projectedCovariance);

            var stdDiag = new Mat(std.Length, std.Length, CvType.CV_32F, new Scalar(0));
            for (var i = 0; i < std.Length; i++)
            {
                stdDiag.put(i, i, std[i]);
            }
            Core.multiply(stdDiag, stdDiag, stdDiag);
            Core.add(projectedCovariance, stdDiag, projectedCovariance);
        }

        private static Mat RectToXyAh(IRectBase rect)
        {
            var xyAh = new Mat(1, 4, CvType.CV_32F);
            xyAh.put(0, 0, new[] {
                rect.Left + rect.Width / 2,
                rect.Top + rect.Height / 2,
                rect.Width / rect.Height,
                rect.Height
            });
            return xyAh;
        }

        private static IRectBase XyAhToRect(Mat xyAh)
        {
            var xyAhArray = new float[4];
            xyAh.get(0, 0, xyAhArray);
            var xyAhWidth = xyAhArray[2] * xyAhArray[3];
            return new TlwhRect(xyAhArray[1] - xyAhArray[3] / 2, xyAhArray[0] - xyAhWidth / 2, xyAhWidth, xyAhArray[3]);
        }
    }
}
