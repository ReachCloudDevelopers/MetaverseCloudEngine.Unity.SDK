#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)

using System;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using OpenCVRange = OpenCVForUnity.CoreModule.Range;
using OpenCVRect = OpenCVForUnity.CoreModule.Rect;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public class YOLOSegmentPredictor : IYoloModel
    {
        Size input_size;
        float conf_threshold;
        float nms_threshold;
        int topK;
        bool upsample;
        int backend;
        int target;

        int num_classes = 80;
        int num_masks = 32;
        bool class_agnostic = false;// Non-use of multi-class NMS

        Net segmentation_net;
        List<string> classNames;
        List<Scalar> palette;

        public YOLOSegmentPredictor(
            string modelFilepath, 
            string classesFilepath,
            Size inputSize, 
            float confThreshold = 0.25f,
            float nmsThreshold = 0.45f,
            int topK = 300,
            bool upsample = true, 
            int backend = Dnn.DNN_BACKEND_OPENCV,
            int target = Dnn.DNN_TARGET_CPU)
        {
            // initialize
            if (!string.IsNullOrEmpty(modelFilepath))
            {
                segmentation_net = Dnn.readNet(modelFilepath);
            }

            if (!string.IsNullOrEmpty(classesFilepath))
            {
                classNames = ReadClassNames(classesFilepath);
                num_classes = classNames.Count;
            }

            input_size = new Size(inputSize.width > 0 ? inputSize.width : 640, inputSize.height > 0 ? inputSize.height : 640);
            conf_threshold = Mathf.Clamp01(confThreshold);
            nms_threshold = Mathf.Clamp01(nmsThreshold);
            this.topK = topK;
            this.upsample = upsample;
            this.backend = backend;
            this.target = target;

            segmentation_net.setPreferableBackend(this.backend);
            segmentation_net.setPreferableTarget(this.target);

            palette = new List<Scalar>();
            palette.Add(new Scalar(255, 56, 56, 255));
            palette.Add(new Scalar(255, 157, 151, 255));
            palette.Add(new Scalar(255, 112, 31, 255));
            palette.Add(new Scalar(255, 178, 29, 255));
            palette.Add(new Scalar(207, 210, 49, 255));
            palette.Add(new Scalar(72, 249, 10, 255));
            palette.Add(new Scalar(146, 204, 23, 255));
            palette.Add(new Scalar(61, 219, 134, 255));
            palette.Add(new Scalar(26, 147, 52, 255));
            palette.Add(new Scalar(0, 212, 187, 255));
            palette.Add(new Scalar(44, 153, 168, 255));
            palette.Add(new Scalar(0, 194, 255, 255));
            palette.Add(new Scalar(52, 69, 147, 255));
            palette.Add(new Scalar(100, 115, 255, 255));
            palette.Add(new Scalar(0, 24, 236, 255));
            palette.Add(new Scalar(132, 56, 255, 255));
            palette.Add(new Scalar(82, 0, 133, 255));
            palette.Add(new Scalar(203, 56, 255, 255));
            palette.Add(new Scalar(255, 149, 200, 255));
            palette.Add(new Scalar(255, 55, 199, 255));
        }

        protected virtual Mat preprocess(Mat image)
        {
            // https://github.com/ultralytics/ultralytics/blob/d74a5a9499acf1afd13d970645e5b1cfcadf4a8f/ultralytics/data/augment.py#L645
            // Add padding to make it input size.
            // (padding to center the image)
            var ratio = Mathf.Max((float)image.cols() / (float)input_size.width, (float)image.rows() / (float)input_size.height);
            var padw = (int)Mathf.Ceil((float)input_size.width * ratio);
            var padh = (int)Mathf.Ceil((float)input_size.height * ratio);
            var paddedImg = new Mat(padh, padw, image.type(), Scalar.all(114));
            if (paddedImg.width() != padw || paddedImg.height() != padh)
            {
                paddedImg.create(padh, padw, image.type());
                Imgproc.rectangle(paddedImg, new OpenCVRect(0, 0, paddedImg.width(), paddedImg.height()), Scalar.all(114), -1);
            }

            var _paddedImg_roi = new Mat(paddedImg, new OpenCVRect((paddedImg.cols() - image.cols()) / 2, (paddedImg.rows() - image.rows()) / 2, image.cols(), image.rows()));
            image.copyTo(_paddedImg_roi);

            var blob = Dnn.blobFromImage(paddedImg, 1.0 / 255.0, input_size, Scalar.all(0), true, false, CvType.CV_32F); // HWC to NCHW, BGR to RGB
            return blob;// [1, 3, h, w]
        }

        public virtual Mat[] Infer(Mat image)
        {
            if (image.channels() != 3)
            {
                Debug.Log("The input image must be in BGR format.");
                return Array.Empty<Mat>();
            }

            // Preprocess
            var input_blob = preprocess(image);

            // Forward
            segmentation_net.setInput(input_blob);

            var output_blob = new List<Mat>();
            segmentation_net.forward(output_blob, segmentation_net.getUnconnectedOutLayersNames());

            // Postprocess
            var det = postprocess(output_blob[0], image.size());

            // process_mask
            var proto = output_blob[1];
            var proto_s1xs2xs3 = proto.reshape(1, new int[] { proto.size(1), proto.size(2), proto.size(3) });// [1, 32, 160, 160] => [32, 160, 160]
            var masks_in = det.colRange(new OpenCVRange(6, 38));
            var bboxes = det.colRange(new OpenCVRange(0, 4));
            var masks = process_mask(proto_s1xs2xs3, masks_in, bboxes, input_size, upsample);

            // scale_boxes
            var det_c0_c6 = det.colRange(0, 6).clone();
            var ratio = Mathf.Max((float)image.cols() / (float)input_size.width, (float)image.rows() / (float)input_size.height);
            var x_factor = ratio;
            var y_factor = ratio;
            var x_shift = ((float)input_size.width * ratio - (float)image.size().width) / 2f;
            var y_shift = ((float)input_size.height * ratio - (float)image.size().height) / 2f;

            for (var i = 0; i < det.rows(); ++i)
            {
                var det_arr = new float[4];
                det_c0_c6.get(i, 0, det_arr);
                var x1 = Mathf.Round(det_arr[0] * x_factor - x_shift);
                var y1 = Mathf.Round(det_arr[1] * y_factor - y_shift);
                var x2 = Mathf.Round(det_arr[2] * x_factor - x_shift);
                var y2 = Mathf.Round(det_arr[3] * y_factor - y_shift);

                det_c0_c6.put(i, 0, new float[] { x1, y1, x2, y2 });
            }

            input_blob.Dispose();
            for (var i = 0; i < output_blob.Count; i++)
            {
                output_blob[i].Dispose();
            }

            det.Dispose();

            var results = new Mat[2];
            results[0] = det_c0_c6;
            results[1] = masks;
            return results;

            // results[0] = [n, 6] (xyxy, conf, cls)
            // results[1] = [n, 160, 160] or [n, 640, 640] (masks) 
        }

        protected virtual Mat postprocess(Mat output_blob, Size original_shape)
        {
            var output_blob_0 = output_blob;

            // 1*116*8400 -> 1*8400*116
            var order = new MatOfInt(0, 2, 1);
            Core.transposeND(output_blob_0, order, output_blob_0);

            if (output_blob_0.size(2) != 4 + num_classes + num_masks)
            {
                Debug.LogWarning("The number of classes and output shapes are different. " +
                                "( output_blob_0.size(2):" + output_blob_0.size(2) + " != 4 + num_classes:" + num_classes + " + " + num_masks + " )\n" +
                                "When using a custom model, be sure to set the correct number of classes by loading the appropriate custom classesFile.");

                num_classes = output_blob_0.size(2) - 4 - num_masks;
            }

            var num = output_blob_0.size(1);
            var output_blob_numx116 = output_blob_0.reshape(1, num);
            var box_delta = output_blob_numx116.colRange(new OpenCVRange(0, 4));
            var classes_scores_delta = output_blob_numx116.colRange(new OpenCVRange(4, 4 + num_classes));
            var mask_delta = output_blob_numx116.colRange(new OpenCVRange(4 + num_classes, 4 + num_classes + num_masks));

            // pre-NMS
            // Pick up rows to process by conf_threshold value and calculate scores and class_ids.
            var pickup_blob_numx6mask = new Mat(300, 6 + num_masks, CvType.CV_32FC1, new Scalar(0));
            Imgproc.rectangle(pickup_blob_numx6mask, new OpenCVRect(4, 0, 1, pickup_blob_numx6mask.rows()), Scalar.all(0), -1);

            var ind = 0;
            for (var i = 0; i < num; ++i)
            {
                var cls_scores = classes_scores_delta.row(i);
                var minmax = Core.minMaxLoc(cls_scores);
                var conf = (float)minmax.maxVal;

                if (conf > conf_threshold)
                {
                    if (ind > pickup_blob_numx6mask.rows())
                    {
                        var _conf_blob_numx6 = new Mat(pickup_blob_numx6mask.rows() * 2, pickup_blob_numx6mask.cols(), pickup_blob_numx6mask.type(), new Scalar(0));
                        pickup_blob_numx6mask.copyTo(_conf_blob_numx6.rowRange(0, pickup_blob_numx6mask.rows()));
                        pickup_blob_numx6mask = _conf_blob_numx6;
                    }

                    var box_arr = new float[4];
                    box_delta.get(i, 0, box_arr);

                    pickup_blob_numx6mask.put(ind, 0, new float[] { box_arr[0], box_arr[1], box_arr[2], box_arr[3], conf, (float)minmax.maxLoc.x });

                    var mask_arr = new float[num_masks];
                    mask_delta.get(i, 0, mask_arr);
                    pickup_blob_numx6mask.put(ind, 6, mask_arr);

                    ind++;
                }
            }

            var num_pickup = pickup_blob_numx6mask.rows();
            var pickup_box_delta = pickup_blob_numx6mask.colRange(new OpenCVRange(0, 4));
            var pickup_confidence = pickup_blob_numx6mask.colRange(new OpenCVRange(4, 5));

            // Convert boxes from [cx, cy, w, h] to [x, y, w, h] where Rect2d data style.
            var boxesMat = new Mat(num_pickup, 4, CvType.CV_32FC1);
            var cxy_delta = pickup_box_delta.colRange(new OpenCVRange(0, 2));
            var wh_delta = pickup_box_delta.colRange(new OpenCVRange(2, 4));
            var xy1 = boxesMat.colRange(new OpenCVRange(0, 2));
            var xy2 = boxesMat.colRange(new OpenCVRange(2, 4));
            wh_delta.copyTo(xy2);
            Core.divide(wh_delta, new Scalar(2.0), wh_delta);
            Core.subtract(cxy_delta, wh_delta, xy1);

            var boxes_m_c4 = new Mat(num_pickup, 1, CvType.CV_64FC4);
            var confidences_m = new Mat(num_pickup, 1, CvType.CV_32FC1);
            var boxes = new MatOfRect2d(boxes_m_c4);
            var confidences = new MatOfFloat(confidences_m);

            // non-maximum suppression
            var boxes_m_c1 = boxes_m_c4.reshape(1, num_pickup);
            boxesMat.convertTo(boxes_m_c1, CvType.CV_64F);
            pickup_confidence.copyTo(confidences_m);

            var indices = new MatOfInt();

            if (class_agnostic)
            {
                // NMS
                Dnn.NMSBoxes(boxes, confidences, conf_threshold, nms_threshold, indices, 1f, topK);
            }
            else
            {
                var pickup_class_ids = pickup_blob_numx6mask.colRange(new OpenCVRange(5, 6));
                var class_ids_m = new Mat(num_pickup, 1, CvType.CV_32SC1);
                var class_ids = new MatOfInt(class_ids_m);

                pickup_class_ids.convertTo(class_ids_m, CvType.CV_32S);

                // multi-class NMS
                Dnn.NMSBoxesBatched(boxes, confidences, class_ids, conf_threshold, nms_threshold, indices, 1f, topK);
            }

            var results = new Mat(indices.rows(), 6 + num_masks, CvType.CV_32FC1);

            for (var i = 0; i < indices.rows(); ++i)
            {
                var idx = (int)indices.get(i, 0)[0];

                pickup_blob_numx6mask.row(idx).copyTo(results.row(i));

                var bbox_arr = new float[4];
                boxesMat.get(idx, 0, bbox_arr);
                var x = bbox_arr[0];
                var y = bbox_arr[1];
                var w = bbox_arr[2];
                var h = bbox_arr[3];
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

        protected virtual Mat process_mask(Mat protos, Mat masks_in, Mat bboxes, Size shape, bool upsample = false)
        {
            if (masks_in.rows() < 1 || bboxes.rows() < 1)
                return new Mat();

            var c = protos.size(0);// 32
            var mh = protos.size(1);// 160
            var mw = protos.size(2);// 160

            var ih = (int)shape.height;// 640
            var iw = (int)shape.width;// 640

            var masks_nxmhmw = new Mat();

            using (var protos_cxmhmw = protos.reshape(1, c))
            {
                Core.gemm(masks_in, protos_cxmhmw, 1, new Mat(), 0, masks_nxmhmw);
            }

            Sigmoid(masks_nxmhmw);

            var masks = masks_nxmhmw.reshape(1, new int[] { masks_in.rows(), mh, mw });

            using (var downsampled_bboxes = bboxes.clone())
            using (var db_c4 = downsampled_bboxes.reshape(4, downsampled_bboxes.rows()))
            {
                Core.multiply(db_c4, new Scalar((float)mw / iw, (float)mh / ih, (float)mw / iw, (float)mh / ih), db_c4);

                CropMask(masks, downsampled_bboxes);//[3, 160, 160]
            }

            if (upsample)
            {
                var masks_n = masks.size(0);// 3
                var masks_h = masks.size(1);// 160
                var masks_w = masks.size(2);// 160

                var upsample_masks = new Mat(new int[] { masks_n, ih, iw }, CvType.CV_8UC1);// [n, 640, 640]
                var upsample_masks_1xihiw_32FC1 = new Mat(1, ih * iw, CvType.CV_32FC1);// [1, 409600]

                for (var i = 0; i < masks_n; ++i)
                {
                    var m_ihiw = masks.row(i).reshape(1, masks_h);// [1, 25600] => [160, 160]
                    var um_ihxiw_32FC1 = upsample_masks_1xihiw_32FC1.reshape(1, ih);// [1, 409600] => [640, 640]
                    Imgproc.resize(m_ihiw, um_ihxiw_32FC1, new Size(iw, ih), -1, -1, Imgproc.INTER_LINEAR);

                    Imgproc.threshold(upsample_masks_1xihiw_32FC1, upsample_masks_1xihiw_32FC1, 0.5f, 1f, Imgproc.THRESH_BINARY);

                    var um_1xihiw = upsample_masks.row(i).reshape(1, 1);// [1, 409600]
                    upsample_masks_1xihiw_32FC1.convertTo(um_1xihiw, CvType.CV_8U, 255.0);
                }
                masks.Dispose();
                masks_nxmhmw.Dispose();

                return upsample_masks;// [n, ih, iw]
            }
            else
            {
                Imgproc.threshold(masks_nxmhmw, masks_nxmhmw, 0.5f, 1f, Imgproc.THRESH_BINARY);

                var masks_8UC1 = new Mat();
                masks.convertTo(masks_8UC1, CvType.CV_8U, 255.0);

                masks.Dispose();
                masks_nxmhmw.Dispose();

                return masks_8UC1;// [n, 160, 160]
            }
        }

        protected virtual void Sigmoid(Mat mat)
        {
            //python: 1 / (1 + np.exp(-x))

            Core.multiply(mat, Scalar.all(-1), mat);
            Core.exp(mat, mat);
            Core.add(mat, Scalar.all(1f), mat);
            using var m = new Mat(mat.size(), mat.type(), Scalar.all(1f));
            Core.divide(m, mat, mat);
        }

        protected virtual Mat CropMask(Mat masks, Mat bxs)
        {
            var n = masks.size(0);// 3
            var h = masks.size(1);// 160
            var w = masks.size(2);// 160

            using var all_0 = new Mat(h, w, CvType.CV_32FC1, Scalar.all(0f));
            using var c_mask = new Mat(h, w, CvType.CV_8UC1);

            using (var masks_nxhw = masks.reshape(1, n))// [n, 160, 160] => [n, 25600]
            {
                for (var i = 0; i < n; ++i)
                {
                    var b_arr = new float[4];
                    bxs.row(i).get(0, 0, b_arr);
                    Imgproc.rectangle(c_mask, new OpenCVRect(0, 0, w, h), Scalar.all(1), -1);
                    Imgproc.rectangle(c_mask, new Point(b_arr[0], b_arr[1]), new Point(b_arr[2], b_arr[3]), Scalar.all(0), -1);

                    var m_hxw = masks_nxhw.row(i).reshape(1, h);// [1, 25600] => [160, 160]
                    all_0.copyTo(m_hxw, c_mask);
                }
            }

            return masks;// [n, 160, 160]
        }

        public virtual void Visualize(Mat image, Mat results, bool isRGB = false)
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

                var c = palette[classId % palette.Count];
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

        public virtual void VisualizeMasks(Mat image, Mat det, Mat masks, float alpha = 0.5f, bool isRGB = false)
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
            var mask_w = (int)(w * image.width() / maxSize);
            var mask_h = (int)(h * image.height() / maxSize);
            var mask_pad_x = (int)((w - mask_w) / 2f);
            var mask_pad_y = (int)((h - mask_h) / 2f);

            var maskMat = new Mat(image.size(), image.type());
            if (maskMat.width() != image.width() || maskMat.height() != image.height())
                maskMat.create(image.size(), image.type());
            var colorMat = new Mat(image.size(), image.type());
            if (colorMat.width() != image.width() || colorMat.height() != image.height())
                colorMat.create(image.size(), image.type());

            using var masks_nxhw = masks.reshape(1, n);
            for (var i = n - 1; i >= 0; --i)
            {

                var cls = new float[1];
                det.get(i, 5, cls);
                var classId = (int)cls[0];
                var c = palette[classId % palette.Count];
                var color = isRGB ? c : new Scalar(c.val[2], c.val[1], c.val[0], c.val[3]);

                var m_hxw = masks_nxhw.row(i).reshape(1, h);// [1, 25600] => [160, 160]
                var m_hxw_roi = new Mat(m_hxw, new OpenCVRect(mask_pad_x, mask_pad_y, mask_w, mask_h));
                Imgproc.resize(m_hxw_roi, maskMat, image.size(), -1, -1, Imgproc.INTER_LINEAR);


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

        public virtual void Dispose()
        {
            segmentation_net?.Dispose();
        }

        protected virtual List<string> ReadClassNames(string filename)
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


        public virtual IYoloModel.DetectionData[] GetObjectRects(Mat objects)
        {
            if (objects.empty())
                return Array.Empty<IYoloModel.DetectionData>();
            var dst = new IYoloModel.DetectionData[objects.rows()];
            MatUtils.copyFromMat(objects, dst);
            return dst;
        }

        public virtual string GetClassLabel(float id)
        {
            var classId = (int)id;
            var className = string.Empty;
            if (classNames != null && classNames.Count != 0)
            {
                if (classId >= 0 && classId < classNames.Count)
                    className = classNames[classId];
            }
            if (string.IsNullOrEmpty(className))
                className = classId.ToString();
            return className;
        }
    }
}
#endif