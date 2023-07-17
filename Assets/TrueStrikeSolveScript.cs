using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

public class TrueStrikeSolveScript : MonoBehaviour
{
    [SerializeField]
    private GameObject _sl;

    private KMBombModule _module;
    private KMSelectable _button;
    private bool _isSolved;
    private int _id = ++_idc;
    private static int _idc;
    private GameObject _strikeSl;
    private static Type _bombType;
    private static bool _suppressDetonation;

#if !UNITY_EDITOR
    static TrueStrikeSolveScript()
    {
        Harmony harm = new Harmony("BDB.StrikeSolve");
        System.Reflection.MethodInfo method = (_bombType = ReflectionHelper.FindTypeInGame("Bomb")).Method("OnStrike");
        System.Reflection.MethodInfo transpiler = typeof(TrueStrikeSolveScript).Method("Weave");
        harm.Patch(method, transpiler: new HarmonyMethod(transpiler));
        Debug.Log("[Strike\\Solve] Harmony patch applied sucessfully.");
    }
#endif

    private IEnumerator Start()
    {
        _module = GetComponent<KMBombModule>();
        _button = GetComponent<KMSelectable>().Children[0];
        _button.OnInteract += () => { Button(); return false; };
        _strikeSl = Instantiate(_sl, _sl.transform.parent);
        _strikeSl.transform.localPosition = new Vector3(-0.075167f, 0.01986f, -0.076057f);
        yield return null;
        _strikeSl.transform.GetChild(1).gameObject.SetActive(false); // Remove duplicate that get created in StatusLight.Start()
    }

    private void Button()
    {
        _button.AddInteractionPunch();

        Debug.Log("[Strike\\Solve #" + _id + "] Button pressed, striking" + (_isSolved ? "." : " and solving."));

        _suppressDetonation = true;
        _module.HandleStrike();
        _suppressDetonation = false;
        if (!_isSolved)
        {
            GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            _module.HandlePass();
            _strikeSl.transform.GetChild(0).GetChild(0).gameObject.SetActive(false);
            _strikeSl.transform.GetChild(0).GetChild(2).gameObject.SetActive(true);
        }
    }

    private static IEnumerable<CodeInstruction> Weave(IEnumerable<CodeInstruction> ils)
    {
        List<CodeInstruction> il = new List<CodeInstruction>(ils);
        System.Reflection.FieldInfo field = _bombType.GetField("NumStrikesToLose", ReflectionHelper.Flags);
        int ix;
        for (ix = 0; ix < il.Count - 1; ix++)
            if (il[ix].LoadsField(field) && il[ix + 1].LoadsConstant())
                break;
        System.Reflection.Emit.Label? noDetoLabel;
        for(int i = ix; i < il.Count; i++)
        {
            if(il[i].Branches(out noDetoLabel))
            {
                il[i].opcode = System.Reflection.Emit.OpCodes.Blt_Un; // Change == to >= so strikes beyond the limit detonate
                il.Insert(i + 1, new CodeInstruction(System.Reflection.Emit.OpCodes.Call, typeof(TrueStrikeSolveScript).Method("IsSuppressed")));
                il.Insert(i + 2, new CodeInstruction(System.Reflection.Emit.OpCodes.Brtrue, noDetoLabel));
                break;
            }
        }

        return il;
    }

    private static bool IsSuppressed()
    {
        return _suppressDetonation;
    }
}
