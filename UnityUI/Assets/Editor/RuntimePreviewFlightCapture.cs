using System;
using System.IO;
using UnityEngine;

namespace XingShang.UI.Authoring
{
    internal sealed class RuntimePreviewDeferredFlightCapture : IDisposable
    {
        internal Texture2D Image;
        internal RuntimePreviewInteractionSample Sample;

        public void Dispose()
        {
            if (Image != null)
                UnityEngine.Object.Destroy(Image);
            Image = null;
        }
    }

    public sealed partial class RuntimePreviewInteractionRunner
    {
        internal RuntimePreviewDeferredFlightCapture CaptureFlightFrame(
            RuntimePreviewInteractionContext context,
            string label,
            float start,
            string note)
        {
            RuntimePreviewInteractionSample sample = new RuntimePreviewInteractionSample
            {
                label = label,
                captureMode = "native-png-flight-texture-deferred-encoding",
                elapsedMs = context.ElapsedMs(start),
                note = note,
                png = Path.Combine(_options.OutputPath, context.Result.name + "-" + Sanitize(label) + "-" +
                    context.Width + "x" + context.Height + ".png"),
                semantics = string.Empty
            };
            context.Result.samples.Add(sample);
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                if (_target == null || _target.width != context.Width || _target.height != context.Height)
                    throw new InvalidOperationException("Flight capture target does not match the native viewport.");
                PrepareCanvases();
                Canvas.ForceUpdateCanvases();
                _camera.Render();
                image = new Texture2D(context.Width, context.Height, TextureFormat.RGBA32, false, false);
                previous = RenderTexture.active;
                RenderTexture.active = _target;
                image.ReadPixels(new Rect(0f, 0f, context.Width, context.Height), 0, 0);
                image.Apply(false, false);
            }
            catch (Exception exception)
            {
                if (image != null)
                    Destroy(image);
                image = null;
                context.Fail(Unwind(exception).ToString());
            }
            finally
            {
                RenderTexture.active = previous;
            }
            return new RuntimePreviewDeferredFlightCapture { Image = image, Sample = sample };
        }

        internal void CompleteFlightCapture(
            RuntimePreviewInteractionContext context,
            RuntimePreviewDeferredFlightCapture capture)
        {
            if (capture == null)
                return;
            try
            {
                if (capture.Image == null)
                    return;
                File.WriteAllBytes(capture.Sample.png, ImageConversion.EncodeToPNG(capture.Image));
                capture.Sample.image = VisualValidation.ValidatePng(
                    capture.Sample.png, context.Width, context.Height);
                context.Check(capture.Sample.image.dimensionsValid,
                    "Flight PNG dimensions mismatch: " + capture.Sample.png);
                context.Check(capture.Sample.image.hasVisiblePixels,
                    "Flight PNG is fully transparent: " + capture.Sample.png);
                context.Check(capture.Sample.image.hasNonUniformContent,
                    "Flight PNG is blank/uniform: " + capture.Sample.png);
            }
            catch (Exception exception)
            {
                context.Fail(Unwind(exception).ToString());
            }
            finally
            {
                capture.Dispose();
            }
        }
    }
}
