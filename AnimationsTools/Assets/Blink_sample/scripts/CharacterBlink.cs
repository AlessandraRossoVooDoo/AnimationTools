using UnityEngine;

namespace Gumbug.Kumite
{
    public class CharacterBlink : MonoBehaviour
    {
        [SerializeField]
        private Vector2 blinkStartRange = new Vector2(30, 200);

        [SerializeField]
        private Vector2 blinkDurationRange = new Vector2(5, 8);

        [SerializeField]
        private int transitionFrames = 6;

        [SerializeField]
        private new bool enabled;

        [SerializeField]
        private Transform leftEye;
        // here you link the eye joint (e: left_eye, inside the rig)

        [SerializeField]
        private Transform rightEye;
        // here you link the eye joint (e: right_eye, inside the rig)

        [SerializeField]
        private Vector2 jitterRangeX = new Vector2(-20, 20);

        [SerializeField]
        private Vector2 jitterRangeY = new Vector2(-20, 20);

        [SerializeField]
        private Vector2 jitterRangeZ = new Vector2(-20, 20);

        [SerializeField]
        private Vector2 jitterFrequencyRange = new Vector2(20, 60);

        private SkinnedMeshRenderer skinnedMeshRenderer;
        private int blinkTick;
        private int eyeJitterTick;
        private int blinkStartFrame;
        private int blinkDuration;
        private int leftLidUpIndex;
        private int leftLidDownIndex;
        private int rightLidUpIndex;
        private int rightLidDownIndex;
        private int blinkEndFrame;

        private Vector3 jitter;
        private int frequency;

        public void Enable()
        {
            enabled = true;
        }

        public void Disable()
        {
            enabled = false;
        }

        private void Start()
        {
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            blinkStartFrame = (int) Random.Range(blinkStartRange.x, blinkStartRange.y);
            blinkDuration = (int) Random.Range(blinkDurationRange.x, blinkDurationRange.y) + transitionFrames*2;
            blinkEndFrame = blinkStartFrame + blinkDuration;
            leftLidUpIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("fighter:blendShape4.Head_LeftLidUp");
            leftLidDownIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("fighter:blendShape4.Head_LeftLidDown");
            rightLidUpIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("fighter:blendShape4.Head_RightLidUp");
            rightLidDownIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex("fighter:blendShape4.Head_RightLidDown");
        }

        private void LateUpdate()
        {
            if (!enabled || Time.timeScale <= 0f)
                return;

            if (blinkTick++>blinkStartFrame)
            {
                var blinkFrame = blinkTick - blinkStartFrame;

                float weight;
                if (blinkFrame < transitionFrames)
                    weight = Mathf.Lerp(0, 100, (float) blinkFrame/transitionFrames);
                else if (blinkFrame > blinkDuration - transitionFrames)
                    weight = Mathf.Lerp(100, 0, ((float) blinkFrame - (blinkDuration - transitionFrames))/transitionFrames);
                else
                    weight = 100;

                if (leftLidDownIndex > -1)
                    skinnedMeshRenderer.SetBlendShapeWeight(leftLidDownIndex, weight);
                if (leftLidUpIndex > -1)
                    skinnedMeshRenderer.SetBlendShapeWeight(leftLidUpIndex, weight);
                if (rightLidDownIndex > -1)
                    skinnedMeshRenderer.SetBlendShapeWeight(rightLidDownIndex, weight);
                if (rightLidUpIndex > -1)
                    skinnedMeshRenderer.SetBlendShapeWeight(rightLidUpIndex, weight);

                if (blinkTick > blinkEndFrame)
                {
                    blinkTick = 0;
                    blinkStartFrame = (int) Random.Range(blinkStartRange.x, blinkStartRange.y);
                    blinkDuration = (int) Random.Range(blinkDurationRange.x, blinkDurationRange.y) + transitionFrames*2;
                    blinkEndFrame = blinkStartFrame + blinkDuration;

                    eyeJitterTick = frequency;
                }
            }

            if (leftEye == null || rightEye == null)
                return;

            if (eyeJitterTick++ >= frequency)
            {
                frequency = (int) GetRandom(jitterFrequencyRange);
                jitter = new Vector3(GetRandom(jitterRangeX), GetRandom(jitterRangeY), GetRandom(jitterRangeZ));
                eyeJitterTick = 0;
            }

            var left = leftEye.rotation.eulerAngles;
            left = left + new Vector3(jitter.x, jitter.y, jitter.z);
            leftEye.rotation = Quaternion.Euler(left);

            var right = rightEye.rotation.eulerAngles;
            right = right + new Vector3(-jitter.x, jitter.y, -jitter.z);
            rightEye.rotation = Quaternion.Euler(right);
        }

        private float GetRandom(Vector2 input)
        {
            return Random.Range(input.x, input.y);
        }
    }
}