#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityEngine;
using OpenCVRange = OpenCVForUnity.CoreModule.Range;
using OpenCVRect = OpenCVForUnity.CoreModule.Rect;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public sealed class YOLOWorldFastSamPredictor : IYoloModel
    {
        private const int NUM_MASKS = 32;
        private const bool CLASS_AGNOSTIC = false; // Non-use of multi-class NMS

        private readonly Size _inputSize;
        private readonly float _confThreshold;
        private readonly float _nmsThreshold;
        private readonly int _topK;
        private readonly bool _upsample;

        private int _numClasses;

        private readonly Net _yoloWorldNet;
        private readonly Net _fastSamNet;
        private readonly List<string> _classNames;
        private readonly List<Scalar> _palette;

        public YOLOWorldFastSamPredictor(
            string yoloWorldModelFilepath,
            string fastSamModelFilepath,
            string classesFilepath,
            Size inputSize,
            float confThreshold = 0.25f,
            float nmsThreshold = 0.45f,
            int topK = 300,
            bool upsample = true)
        {
            _yoloWorldNet = Dnn.readNet(yoloWorldModelFilepath);
            _fastSamNet = Dnn.readNet(fastSamModelFilepath);

            _classNames = ReadClassNames(classesFilepath);
            _numClasses = _classNames.Count;

            _inputSize = new Size(inputSize.width > 0 ? inputSize.width : 640, inputSize.height > 0 ? inputSize.height : 640);
            _confThreshold = Mathf.Clamp01(confThreshold);
            _nmsThreshold = Mathf.Clamp01(nmsThreshold);
            _topK = topK;
            _upsample = upsample;

            _yoloWorldNet.setPreferableBackend(Dnn.DNN_BACKEND_OPENCV);
            _yoloWorldNet.setPreferableTarget(Dnn.DNN_TARGET_CPU);
            
            _fastSamNet.setPreferableBackend(Dnn.DNN_BACKEND_OPENCV);
            _fastSamNet.setPreferableTarget(Dnn.DNN_TARGET_CPU);

            _palette = new List<Scalar>
            {
                new(255, 56, 56, 255),
                new(255, 157, 151, 255),
                new(255, 112, 31, 255),
                new(255, 178, 29, 255),
                new(207, 210, 49, 255),
                new(72, 249, 10, 255),
                new(146, 204, 23, 255),
                new(61, 219, 134, 255),
                new(26, 147, 52, 255),
                new(0, 212, 187, 255),
                new(44, 153, 168, 255),
                new(0, 194, 255, 255),
                new(52, 69, 147, 255),
                new(100, 115, 255, 255),
                new(0, 24, 236, 255),
                new(132, 56, 255, 255),
                new(82, 0, 133, 255),
                new(203, 56, 255, 255),
                new(255, 149, 200, 255),
                new(255, 55, 199, 255)
            };
        }

        private Mat Preprocess(Mat image)
        {
            // https://github.com/ultralytics/ultralytics/blob/d74a5a9499acf1afd13d970645e5b1cfcadf4a8f/ultralytics/data/augment.py#L645
            // Add padding to make it input size.
            // (padding to center the image)
            var ratio = Mathf.Max((float)image.cols() / (float)_inputSize.width, (float)image.rows() / (float)_inputSize.height);
            var padW = (int)Mathf.Ceil((float)_inputSize.width * ratio);
            var padH = (int)Mathf.Ceil((float)_inputSize.height * ratio);
            var paddedImg = new Mat(padH, padW, image.type(), Scalar.all(114));
            if (paddedImg.width() != padW || paddedImg.height() != padH)
            {
                paddedImg.create(padH, padW, image.type());
                Imgproc.rectangle(paddedImg, new OpenCVRect(0, 0, paddedImg.width(), paddedImg.height()), Scalar.all(114), -1);
            }

            var paddedImgRoi = new Mat(paddedImg, new OpenCVRect((paddedImg.cols() - image.cols()) / 2, (paddedImg.rows() - image.rows()) / 2, image.cols(), image.rows()));
            image.copyTo(paddedImgRoi);

            var blob = Dnn.blobFromImage(paddedImg, 1.0 / 255.0, _inputSize, Scalar.all(0), true, false, CvType.CV_32F); // HWC to NCHW, BGR to RGB
            return blob;// [1, 3, h, w]
        }

        public Mat[] Infer(Mat image)
        {
            if (image.channels() != 3)
            {
                Debug.Log("The input image must be in BGR format.");
                return Array.Empty<Mat>();
            }

            // Preprocess
            var inputBlob = Preprocess(image);

            // Objs
            _yoloWorldNet.setInput(inputBlob);
            var yoloOutputBlob = new List<Mat>();
            _yoloWorldNet.forward(yoloOutputBlob, _yoloWorldNet.getUnconnectedOutLayersNames());
            var yoloDetections = PostprocessYolo(yoloOutputBlob[0]);
            
            // Masks
            _fastSamNet.setInput(inputBlob);
            var samOutputBlob = new List<Mat>();
            _fastSamNet.forward(samOutputBlob);

            // process_mask
            var proto = samOutputBlob[0];
            var protoS1Xs2Xs3 = proto.reshape(1, new int[] { proto.size(1), proto.size(2), proto.size(3) });// [1, 32, 160, 160] => [32, 160, 160]
            var masksIn = yoloDetections.colRange(new OpenCVRange(6, 38));
            var bBoxes = yoloDetections.colRange(new OpenCVRange(0, 4));
            var masks = ProcessMask(protoS1Xs2Xs3, masksIn, bBoxes, _inputSize, _upsample);

            // scale_boxes
            var detectionC0C6 = yoloDetections.colRange(0, 6).clone();
            var ratio = Mathf.Max(image.cols() / (float)_inputSize.width, (float)image.rows() / (float)_inputSize.height);
            var xShift = ((float)_inputSize.width * ratio - (float)image.size().width) / 2f;
            var yShift = ((float)_inputSize.height * ratio - (float)image.size().height) / 2f;

            for (var i = 0; i < yoloDetections.rows(); ++i)
            {
                var detArr = new float[4];
                detectionC0C6.get(i, 0, detArr);
                var x1 = Mathf.Round(detArr[0] * ratio - xShift);
                var y1 = Mathf.Round(detArr[1] * ratio - yShift);
                var x2 = Mathf.Round(detArr[2] * ratio - xShift);
                var y2 = Mathf.Round(detArr[3] * ratio - yShift);

                detectionC0C6.put(i, 0, new float[] { x1, y1, x2, y2 });
            }

            inputBlob.Dispose();
            foreach (var t in yoloOutputBlob)
                t.Dispose();
            foreach (var t in samOutputBlob)
                t.Dispose();

            yoloDetections.Dispose();

            var results = new Mat[2];
            results[0] = detectionC0C6;
            results[1] = masks;
            return results;

            // results[0] = [n, 6] (xyxy, conf, cls)
            // results[1] = [n, 160, 160] or [n, 640, 640] (masks) 
        }

        private Mat PostprocessYolo(Mat outputBlob)
        {
            // 1*116*8400 -> 1*8400*116
            var order = new MatOfInt(0, 2, 1);
            Core.transposeND(outputBlob, order, outputBlob);

            if (outputBlob.size(2) != 4 + _numClasses + NUM_MASKS)
            {
                Debug.LogWarning("The number of classes and output shapes are different. " +
                                "( output_blob_0.size(2):" + outputBlob.size(2) + " != 4 + num_classes:" + _numClasses + " + " + NUM_MASKS + " )\n" +
                                "When using a custom model, be sure to set the correct number of classes by loading the appropriate custom classesFile.");

                _numClasses = outputBlob.size(2) - 4 - NUM_MASKS;
            }

            var num = outputBlob.size(1);
            var outputBlobNumx116 = outputBlob.reshape(1, num);
            var boxDelta = outputBlobNumx116.colRange(new OpenCVRange(0, 4));
            var classesScoresDelta = outputBlobNumx116.colRange(new OpenCVRange(4, 4 + _numClasses));
            var maskDelta = outputBlobNumx116.colRange(new OpenCVRange(4 + _numClasses, 4 + _numClasses + NUM_MASKS));

            // pre-NMS
            // Pick up rows to process by conf_threshold value and calculate scores and class_ids.
            var pickupBlobNumx6Mask = new Mat(300, 6 + NUM_MASKS, CvType.CV_32FC1, new Scalar(0));
            Imgproc.rectangle(pickupBlobNumx6Mask, new OpenCVRect(4, 0, 1, pickupBlobNumx6Mask.rows()), Scalar.all(0), -1);

            var ind = 0;
            for (var i = 0; i < num; ++i)
            {
                var clsScores = classesScoresDelta.row(i);
                var minmax = Core.minMaxLoc(clsScores);
                var conf = (float)minmax.maxVal;

                if (conf > _confThreshold)
                {
                    if (ind > pickupBlobNumx6Mask.rows())
                    {
                        var confBlobNumx6 = new Mat(pickupBlobNumx6Mask.rows() * 2, pickupBlobNumx6Mask.cols(), pickupBlobNumx6Mask.type(), new Scalar(0));
                        pickupBlobNumx6Mask.copyTo(confBlobNumx6.rowRange(0, pickupBlobNumx6Mask.rows()));
                        pickupBlobNumx6Mask = confBlobNumx6;
                    }

                    var boxArr = new float[4];
                    boxDelta.get(i, 0, boxArr);
                    pickupBlobNumx6Mask.put(ind, 0, new float[] { boxArr[0], boxArr[1], boxArr[2], boxArr[3], conf, (float)minmax.maxLoc.x });

                    var maskArr = new float[NUM_MASKS];
                    maskDelta.get(i, 0, maskArr);
                    pickupBlobNumx6Mask.put(ind, 6, maskArr);

                    ind++;
                }
            }

            var numPickup = pickupBlobNumx6Mask.rows();
            var pickupBoxDelta = pickupBlobNumx6Mask.colRange(new OpenCVRange(0, 4));
            var pickupConfidence = pickupBlobNumx6Mask.colRange(new OpenCVRange(4, 5));

            // Convert boxes from [cx, cy, w, h] to [x, y, w, h] where Rect2d data style.
            var boxesMat = new Mat(numPickup, 4, CvType.CV_32FC1);
            var cxyDelta = pickupBoxDelta.colRange(new OpenCVRange(0, 2));
            var whDelta = pickupBoxDelta.colRange(new OpenCVRange(2, 4));
            var xy1 = boxesMat.colRange(new OpenCVRange(0, 2));
            var xy2 = boxesMat.colRange(new OpenCVRange(2, 4));
            whDelta.copyTo(xy2);
            Core.divide(whDelta, new Scalar(2.0), whDelta);
            Core.subtract(cxyDelta, whDelta, xy1);

            var boxesMC4 = new Mat(numPickup, 1, CvType.CV_64FC4);
            var confidencesM = new Mat(numPickup, 1, CvType.CV_32FC1);
            var boxes = new MatOfRect2d(boxesMC4);
            var confidences = new MatOfFloat(confidencesM);

            // non-maximum suppression
            var boxesMC1 = boxesMC4.reshape(1, numPickup);
            boxesMat.convertTo(boxesMC1, CvType.CV_64F);
            pickupConfidence.copyTo(confidencesM);

            var indices = new MatOfInt();

            if (CLASS_AGNOSTIC)
            {
                // NMS
                Dnn.NMSBoxes(boxes, confidences, _confThreshold, _nmsThreshold, indices, 1f, _topK);
            }
            else
            {
                var pickupClassIds = pickupBlobNumx6Mask.colRange(new OpenCVRange(5, 6));
                var classIdsM = new Mat(numPickup, 1, CvType.CV_32SC1);
                var classIds = new MatOfInt(classIdsM);

                pickupClassIds.convertTo(classIdsM, CvType.CV_32S);

                // multi-class NMS
                Dnn.NMSBoxesBatched(boxes, confidences, classIds, _confThreshold, _nmsThreshold, indices, 1f, _topK);
            }

            var results = new Mat(indices.rows(), 6 + NUM_MASKS, CvType.CV_32FC1);

            for (var i = 0; i < indices.rows(); ++i)
            {
                var idx = (int)indices.get(i, 0)[0];

                pickupBlobNumx6Mask.row(idx).copyTo(results.row(i));

                var bboxArr = new float[4];
                boxesMat.get(idx, 0, bboxArr);
                var x = bboxArr[0];
                var y = bboxArr[1];
                var w = bboxArr[2];
                var h = bboxArr[3];
                results.put(i, 0, new float[] { x, y, x + w, y + h });
            }

            indices.Dispose();

            // [
            //   [xyxy, conf, cls, mask]
            //   ...
            //   [xyxy, conf, cls, mask]
            // ]
            return results;
        }

        private Mat ProcessMask(Mat protos, Mat masksIn, Mat bboxes, Size shape, bool upsample = false)
        {
            if (masksIn.rows() < 1 || bboxes.rows() < 1)
                return new Mat();

            var c = protos.size(0);// 32
            var mh = protos.size(1);// 160
            var mw = protos.size(2);// 160

            var ih = (int)shape.height;// 640
            var iw = (int)shape.width;// 640

            var masksNxmhmw = new Mat();
            using (var protosCxmhmw = protos.reshape(1, c))
                Core.gemm(masksIn, protosCxmhmw, 1, new Mat(), 0, masksNxmhmw);

            Sigmoid(masksNxmhmw);

            var masks = masksNxmhmw.reshape(1, new[] { masksIn.rows(), mh, mw });

            using (var downsampledBboxes = bboxes.clone())
            using (var dbC4 = downsampledBboxes.reshape(4, downsampledBboxes.rows()))
            {
                Core.multiply(dbC4, new Scalar((float)mw / iw, (float)mh / ih, (float)mw / iw, (float)mh / ih), dbC4);
                CropMask(masks, downsampledBboxes);//[3, 160, 160]
            }

            if (upsample)
            {
                var masksN = masks.size(0);// 3
                var masksH = masks.size(1);// 160
                var upsampleMasks = new Mat(new int[] { masksN, ih, iw }, CvType.CV_8UC1);// [n, 640, 640]
                var upsampleMasks1Xihiw32Fc1 = new Mat(1, ih * iw, CvType.CV_32FC1);// [1, 409600]

                for (var i = 0; i < masksN; ++i)
                {
                    var mIhiw = masks.row(i).reshape(1, masksH);// [1, 25600] => [160, 160]
                    var umIhxiw32Fc1 = upsampleMasks1Xihiw32Fc1.reshape(1, ih);// [1, 409600] => [640, 640]
                    Imgproc.resize(mIhiw, umIhxiw32Fc1, new Size(iw, ih), -1, -1, Imgproc.INTER_LINEAR);
                    Imgproc.threshold(upsampleMasks1Xihiw32Fc1, upsampleMasks1Xihiw32Fc1, 0.5f, 1f, Imgproc.THRESH_BINARY);

                    var um1Xihiw = upsampleMasks.row(i).reshape(1, 1);// [1, 409600]
                    upsampleMasks1Xihiw32Fc1.convertTo(um1Xihiw, CvType.CV_8U, 255.0);
                }
                masks.Dispose();
                masksNxmhmw.Dispose();

                return upsampleMasks;// [n, ih, iw]
            }

            Imgproc.threshold(masksNxmhmw, masksNxmhmw, 0.5f, 1f, Imgproc.THRESH_BINARY);

            var masks8Uc1 = new Mat();
            masks.convertTo(masks8Uc1, CvType.CV_8U, 255.0);

            masks.Dispose();
            masksNxmhmw.Dispose();

            return masks8Uc1;// [n, 160, 160]
        }

        private static void Sigmoid(Mat mat)
        {
            //python: 1 / (1 + np.exp(-x))
            Core.multiply(mat, Scalar.all(-1), mat);
            Core.exp(mat, mat);
            Core.add(mat, Scalar.all(1f), mat);
            using var m = new Mat(mat.size(), mat.type(), Scalar.all(1f));
            Core.divide(m, mat, mat);
        }

        private Mat CropMask(Mat masks, Mat bxs)
        {
            var n = masks.size(0);// 3
            var h = masks.size(1);// 160
            var w = masks.size(2);// 160

            using var all0 = new Mat(h, w, CvType.CV_32FC1, Scalar.all(0f));
            using var cMask = new Mat(h, w, CvType.CV_8UC1);
            using var masksNxhw = masks.reshape(1, n);
            
            for (var i = 0; i < n; ++i)
            {
                var bArr = new float[4];
                bxs.row(i).get(0, 0, bArr);
                Imgproc.rectangle(cMask, new OpenCVRect(0, 0, w, h), Scalar.all(1), -1);
                Imgproc.rectangle(cMask, new Point(bArr[0], bArr[1]), new Point(bArr[2], bArr[3]), Scalar.all(0), -1);

                var mHxw = masksNxhw.row(i).reshape(1, h);// [1, 25600] => [160, 160]
                all0.copyTo(mHxw, cMask);
            }

            return masks;// [n, 160, 160]
        }

        public void Visualize(Mat image, Mat results, bool isRGB = false)
        {
            if (image.IsDisposed)
                return;

            if (results.empty() || results.cols() < 6)
                return;

            var data = GetObjectRects(results);

            foreach (var d in data.Reverse())
            {
                var left = d.x1;
                var top = d.y1;
                var right = d.x2;
                var bottom = d.y2;
                var conf = d.conf;
                var classId = (int)d.cls;

                var c = _palette[classId % _palette.Count];
                var color = isRGB ? c : new Scalar(c.val[2], c.val[1], c.val[0], c.val[3]);

                Imgproc.rectangle(image, new Point(left, top), new Point(right, bottom), color, 2);

                var label = $"{GetClassLabel(classId)}, {conf:F2}";

                var baseLine = new int[1];
                var labelSize = Imgproc.getTextSize(label, Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, 1, baseLine);

                top = Mathf.Max((float)top, (float)labelSize.height);
                Imgproc.rectangle(image, new Point(left, top - labelSize.height),
                    new Point(left + labelSize.width, top + baseLine[0]), color, Core.FILLED);
                Imgproc.putText(image, label, new Point(left, top), Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, Scalar.all(255), 1, Imgproc.LINE_AA);
            }
        }

        public void VisualizeMasks(Mat image, Mat det, Mat masks, float alpha = 0.5f, bool isRGB = false)
        {
            if (image.IsDisposed)
                return;

            if (det.empty() || det.cols() < 6)
                return;

            if (masks.empty())
                return;

            var n = masks.size(0);
            var h = masks.size(1);
            var w = masks.size(2);

            var maxSize = Mathf.Max((float)image.size().width, (float)image.size().height);
            var maskW = (int)(w * image.width() / maxSize);
            var maskH = (int)(h * image.height() / maxSize);
            var maskPadX = (int)((w - maskW) / 2f);
            var maskPadY = (int)((h - maskH) / 2f);

            var maskMat = new Mat(image.size(), image.type());
            if (maskMat.width() != image.width() || maskMat.height() != image.height())
                maskMat.create(image.size(), image.type());
            var colorMat = new Mat(image.size(), image.type());
            if (colorMat.width() != image.width() || colorMat.height() != image.height())
                colorMat.create(image.size(), image.type());

            using var masksNxhw = masks.reshape(1, n);
            for (var i = n - 1; i >= 0; --i)
            {

                var cls = new float[1];
                det.get(i, 5, cls);
                var classId = (int)cls[0];
                var c = _palette[classId % _palette.Count];
                var color = isRGB ? c : new Scalar(c.val[2], c.val[1], c.val[0], c.val[3]);

                var mHxw = masksNxhw.row(i).reshape(1, h);// [1, 25600] => [160, 160]
                var mHxwRoi = new Mat(mHxw, new OpenCVRect(maskPadX, maskPadY, maskW, maskH));
                Imgproc.resize(mHxwRoi, maskMat, image.size(), -1, -1, Imgproc.INTER_LINEAR);


                //
                Imgproc.rectangle(colorMat, new OpenCVRect(0, 0, colorMat.width(), colorMat.height()), color, -1);
                Core.addWeighted(colorMat, alpha, image, alpha, 0, colorMat);
                colorMat.copyTo(image, maskMat);
                //
                // or
                ////// use ROI
                //float[] box = new float[4];
                //det.get(i, 0, box);
                //float left = box[0];
                //float top = box[1];
                //float right = box[2];
                //float bottom = box[3];
                //OpenCVRect roi_rect = new OpenCVRect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
                //roi_rect = new OpenCVRect(0, 0, image.width(), image.height()).intersect(roi_rect);

                //using (Mat maskMat_roi = new Mat(maskMat, roi_rect))
                //using (Mat colorMat_roi = new Mat(colorMat, roi_rect))
                //using (Mat image_roi = new Mat(image, roi_rect))
                //{
                //    Imgproc.rectangle(colorMat_roi, new OpenCVRect(0, 0, colorMat_roi.width(), colorMat_roi.height()), color, -1);
                //    Core.addWeighted(colorMat_roi, alpha, image_roi, alpha, 0, colorMat_roi);
                //    colorMat_roi.copyTo(image_roi, maskMat_roi);
                //}
                //
            }
        }

        public void Dispose()
        {
            _yoloWorldNet?.Dispose();
        }

        private static List<string> ReadClassNames(string filename)
        {
            var names = new List<string>();
            System.IO.StreamReader cReader = null;
            try
            {
                cReader = new System.IO.StreamReader(filename, Encoding.Default);

                while (cReader.Peek() >= 0)
                {
                    var name = cReader.ReadLine();
                    names.Add(name);
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

            return names;
        }

        public IYoloModel.DetectionData[] GetObjectRects(Mat results)
        {
            if (results.empty())
                return Array.Empty<IYoloModel.DetectionData>();
            var dst = new IYoloModel.DetectionData[results.rows()];
            MatUtils.copyFromMat(results, dst);
            return dst;
        }

        public string GetClassLabel(float id)
        {
            var classId = (int)id;
            var className = string.Empty;
            if (_classNames != null && _classNames.Count != 0)
            {
                if (classId >= 0 && classId < _classNames.Count)
                    className = _classNames[classId];
            }
            if (string.IsNullOrEmpty(className))
                className = classId.ToString();
            return className;
        }
    }
}
#endif