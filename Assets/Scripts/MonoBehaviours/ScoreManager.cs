using ArcCore.Mathematics;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArcCore.Enumerations;

namespace ArcCore.MonoBehaviours
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public float MAX_VALUE = 10_000_000f;

        [HideInInspector] public int maxCombo;

        public int
            maxPureCount,
            latePureCount,
            earlyPureCount,
            lateFarCount,
            earlyFarCount,
            lostCount,
            currentCombo;

        public static void ResetCombo() => Instance.currentCombo = 0;
        public static void AppendCombo() => Instance.currentCombo++;

        public static void RegisterMPure() { Instance.maxPureCount++; AppendCombo(); }
        public static void RegisterLPure() { Instance.latePureCount++; AppendCombo(); }
        public static void RegisterEPure() { Instance.earlyPureCount++; AppendCombo(); }
        public static void RegisterLFar() { Instance.lateFarCount++; AppendCombo(); }
        public static void RegisterEFar() { Instance.earlyFarCount++; AppendCombo(); }
        public static void RegisterLost() { Instance.lostCount++; ResetCombo(); }

        [HideInInspector] public float currentScore;

        public Text textUI;

        void Awake()
        {
            Instance = this;
        }

        [System.Obsolete]
        public void AddJudge(JudgeType type)
        {
            switch(type)
            {
                case JudgeType.LOST:
                    lostCount++;
                    currentCombo = 0;
                    break;
                case JudgeType.MAX_PURE:
                    maxPureCount++;
                    currentCombo++;
                    break;
                case JudgeType.LATE_PURE:
                    latePureCount++;
                    currentCombo++;
                    break;
                case JudgeType.EARLY_PURE:
                    earlyPureCount++;
                    currentCombo++;
                    break;
                case JudgeType.LATE_FAR:
                    lateFarCount++;
                    currentCombo++;
                    break;
                case JudgeType.EARLY_FAR:
                    earlyFarCount++;
                    currentCombo++;
                    break;
            }
        }

        //call later
        public void UpdateScore()
        {
            currentScore =
                (maxPureCount + latePureCount + earlyPureCount) * MAX_VALUE / maxCombo +
                (lateFarCount + earlyFarCount) * MAX_VALUE / maxCombo / 2 +
                 maxPureCount;
            textUI.text = $"{(int)currentScore:D8}";
        }
    }
}