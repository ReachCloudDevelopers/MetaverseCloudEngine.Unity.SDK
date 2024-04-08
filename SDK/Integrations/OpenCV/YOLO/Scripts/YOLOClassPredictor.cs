#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using OpenCVRect = OpenCVForUnity.CoreModule.Rect;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public sealed class YoloClassPredictor
    {
        private readonly Size _inputSize;
        private int _target;
        private readonly Net _classificationNet;
        private readonly List<string> _classNames;
        private readonly List<Scalar> _palette;
        private Mat _inputSizeMat;
        private Mat _getDataMat;

        public YoloClassPredictor(
            string modelFilepath, 
            string classesFilepath, 
            Size inputSize, 
            int backend = Dnn.DNN_BACKEND_OPENCV, 
            int target = Dnn.DNN_TARGET_CPU)
        {
            // initialize
            if (!string.IsNullOrEmpty(modelFilepath))
                _classificationNet = Dnn.readNet(modelFilepath);

            if (!string.IsNullOrEmpty(classesFilepath))
                _classNames = ReadClassNames(classesFilepath);

            _inputSize = new Size(inputSize.width > 0 ? inputSize.width : 224, inputSize.height > 0 ? inputSize.height : 224);

            _classificationNet!.setPreferableBackend(backend);
            _classificationNet.setPreferableTarget(target);

            _palette = new List<Scalar>
            {
                new (255, 56, 56, 255),
                new (255, 157, 151, 255),
                new (255, 112, 31, 255),
                new (255, 178, 29, 255),
                new (207, 210, 49, 255),
                new (72, 249, 10, 255),
                new (146, 204, 23, 255),
                new (61, 219, 134, 255),
                new (26, 147, 52, 255),
                new (0, 212, 187, 255),
                new (44, 153, 168, 255),
                new (0, 194, 255, 255),
                new (52, 69, 147, 255),
                new (100, 115, 255, 255),
                new (0, 24, 236, 255),
                new (132, 56, 255, 255),
                new (82, 0, 133, 255),
                new (203, 56, 255, 255),
                new (255, 149, 200, 255),
                new (255, 55, 199, 255)
            };
        }

        private Mat Preprocess(Mat image)
        {
            // https://github.com/ultralytics/ultralytics/blob/d74a5a9499acf1afd13d970645e5b1cfcadf4a8f/ultralytics/data/augment.py#L1059
            // Resizes and crops the center of the image using a letterbox method.
            var c = image.channels();
            var h = (int)_inputSize.height;
            var w = (int)_inputSize.width;

            _inputSizeMat ??= new Mat(h, w, CvType.CV_8UC3);

            var imh = image.height();
            var imw = image.width();
            var m = Mathf.Min(imh, imw);
            var top = (int)((imh - m) / 2f);
            var left = (int)((imw - m) / 2f);
            var imageCrop = new Mat(image, new OpenCVRect(0, 0, image.width(), image.height()).intersect(new OpenCVRect(left, top, m, m)));
            Imgproc.resize(imageCrop, _inputSizeMat, new Size(w, h));

            var blob = Dnn.blobFromImage(_inputSizeMat, 1.0 / 255.0, _inputSize, Scalar.all(0), true, false, CvType.CV_32F); // HWC to NCHW, BGR to RGB
            return blob;// [1, 3, h, w]
        }

        public Mat Infer(Mat image)
        {
            // check
            if (image.channels() != 3)
            {
                Debug.Log("The input image must be in BGR format.");
                return new Mat();
            }

            // Preprocess
            var inputBlob = Preprocess(image);

            // Forward
            _classificationNet.setInput(inputBlob);

            var outputBlob = new List<Mat>();
            _classificationNet.forward(outputBlob, _classificationNet.getUnconnectedOutLayersNames());

            // Postprocess
            var results = Postprocess(outputBlob);
            inputBlob.Dispose();
            foreach (var t in outputBlob)
                t.Dispose();

            return results;// [1, num_classes]
        }

        private static Mat Postprocess(IReadOnlyList<Mat> outputBlob)
        {
            var outputBlob0 = outputBlob[0];
            var results = outputBlob0.clone();
            return results;// [1, num_classes]
        }

        public void Visualize(Mat image, Mat results, bool printResults = false, bool isRGB = false)
        {
            if (image.IsDisposed)
                return;

            if (results.empty())
                return;

            StringBuilder sb = null;

            if (printResults)
                sb = new StringBuilder(64);

            var bmData = GetBestMatchData(results);
            var classId = (int)bmData.cls;
            var label = GetClassLabel(bmData.cls) + ", " + bmData.conf.ToString("F2");

            var c = _palette[classId % _palette.Count];
            var color = isRGB ? c : new Scalar(c.val[2], c.val[1], c.val[0], c.val[3]);

            var baseLine = new int[1];
            var labelSize = Imgproc.getTextSize(label, Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, 1, baseLine);

            var top = 20f + (float)labelSize.height;
            var left = (float)(image.width() / 2f - labelSize.width / 2f);
            top = Mathf.Max((float)top, (float)labelSize.height);
            Imgproc.rectangle(image, new Point(left, top - labelSize.height),
                new Point(left + labelSize.width, top + baseLine[0]), color, Core.FILLED);
            Imgproc.putText(image, label, new Point(left, top), Imgproc.FONT_HERSHEY_SIMPLEX, 1.0, Scalar.all(255), 1, Imgproc.LINE_AA);

            // Print results
            if (printResults)
                sb.AppendLine("Best match: " + GetClassLabel(bmData.cls) + ", " + bmData.ToString());

            if (printResults)
                Debug.Log(sb.ToString());
        }

        public void Dispose()
        {
            _classificationNet?.Dispose();
            _inputSizeMat?.Dispose();
            _inputSizeMat = null;
            _getDataMat?.Dispose();
            _getDataMat = null;
        }

        private List<string> ReadClassNames(string filename)
        {
            var classNames = new List<string>();

            System.IO.StreamReader cReader = null;
            try
            {
                cReader = new System.IO.StreamReader(filename, System.Text.Encoding.Default);

                while (cReader.Peek() >= 0)
                {
                    var name = cReader.ReadLine();
                    classNames.Add(name);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.Message);
                return null;
            }
            finally
            {
                cReader?.Close();
            }

            return classNames;
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct ClassificationData
        {
            public readonly float cls;
            public readonly float conf;

            // sizeof(ClassificationData)
            public const int Size = 2 * sizeof(float);

            public ClassificationData(int cls, float conf)
            {
                this.cls = cls;
                this.conf = conf;
            }

            public override string ToString()
            {
                return "cls:" + cls.ToString() + " conf:" + conf.ToString();
            }
        };

        public ClassificationData[] GetData(Mat results)
        {
            if (results.empty())
                return new ClassificationData[0];

            var num = results.cols();

            if (_getDataMat == null)
            {
                _getDataMat = new Mat(num, 2, CvType.CV_32FC1);
                var arange = Enumerable.Range(0, num).Select(i => (float)i).ToArray();
                _getDataMat.col(0).put(0, 0, arange);
            }

            var resultsNumx1 = results.reshape(1, num);
            resultsNumx1.copyTo(_getDataMat.col(1));

            var dst = new ClassificationData[num];
            MatUtils.copyFromMat(_getDataMat, dst);

            return dst;
        }

        public ClassificationData[] GetSortedData(Mat results, int topK = 5)
        {
            if (results.empty())
                return new ClassificationData[0];

            var num = results.cols();

            if (topK < 1 || topK > num) topK = num;
            var sortedData = GetData(results).OrderByDescending(x => x.conf).Take(topK).ToArray();

            return sortedData;
        }

        public ClassificationData GetBestMatchData(Mat results)
        {
            if (results.empty())
                return new ClassificationData();

            var minmax = Core.minMaxLoc(results);

            return new ClassificationData((int)minmax.maxLoc.x, (float)minmax.maxVal);
        }

        public string GetClassLabel(float id)
        {
            var classId = (int)id;
            var className = string.Empty;
            if (_classNames != null && _classNames.Count != 0)
            {
                if (classId >= 0 && classId < _classNames.Count)
                {
                    className = _classNames[classId];
                }
            }
            if (string.IsNullOrEmpty(className))
                className = classId.ToString();

            return className;
        }
    }
}
#endif