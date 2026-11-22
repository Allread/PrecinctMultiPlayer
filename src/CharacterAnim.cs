using System.Collections.Generic;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>
    /// Synchronisation des animations du policier à pied.
    ///
    /// Le jeu anime le personnage via un Animator Unity piloté par des paramètres
    /// (vitesse, direction, accroupi, arme...). Les deux joueurs ont le même
    /// AnimatorController, donc les paramètres sont dans le même ordre : on
    /// envoie leurs valeurs (et le poids des calques), et le modèle distant joue
    /// les mêmes animations, sans avoir besoin de connaître leurs noms.
    /// Les « Triggers » (actions ponctuelles) ne sont pas lisibles : ignorés.
    /// </summary>
    public class AnimatorSync
    {
        public const int MaxValues = 96;

        private readonly Animator _anim;
        private readonly int[] _hashes;
        private readonly AnimatorControllerParameterType[] _types;

        public Animator Animator => _anim;
        public int ParamCount => _hashes.Length;

        public AnimatorSync(Animator anim)
        {
            _anim = anim;
            var hashes = new List<int>();
            var types = new List<AnimatorControllerParameterType>();
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger) continue;
                hashes.Add(p.nameHash);
                types.Add(p.type);
                if (hashes.Count >= MaxValues / 2) break;
            }
            _hashes = hashes.ToArray();
            _types = types.ToArray();
        }

        /// <summary>Valeurs à envoyer : [paramètres..., poids des calques 1..n].</summary>
        public float[] Capture(out byte paramCount)
        {
            int layers = Mathf.Min(Mathf.Max(0, _anim.layerCount - 1), MaxValues - _hashes.Length);
            var v = new float[_hashes.Length + layers];
            for (int i = 0; i < _hashes.Length; i++)
            {
                switch (_types[i])
                {
                    case AnimatorControllerParameterType.Float: v[i] = _anim.GetFloat(_hashes[i]); break;
                    case AnimatorControllerParameterType.Int: v[i] = _anim.GetInteger(_hashes[i]); break;
                    case AnimatorControllerParameterType.Bool: v[i] = _anim.GetBool(_hashes[i]) ? 1f : 0f; break;
                }
            }
            for (int l = 0; l < layers; l++) v[_hashes.Length + l] = _anim.GetLayerWeight(l + 1);
            paramCount = (byte)_hashes.Length;
            return v;
        }

        public void Apply(float[] v, int paramCount, float dt)
        {
            if (v == null) return;
            int n = Mathf.Min(paramCount, _hashes.Length);
            float k = 1f - Mathf.Exp(-12f * dt); // lissage des floats (vitesse, direction)
            for (int i = 0; i < n; i++)
            {
                switch (_types[i])
                {
                    case AnimatorControllerParameterType.Float:
                        _anim.SetFloat(_hashes[i], Mathf.Lerp(_anim.GetFloat(_hashes[i]), v[i], k)); break;
                    case AnimatorControllerParameterType.Int:
                        _anim.SetInteger(_hashes[i], Mathf.RoundToInt(v[i])); break;
                    case AnimatorControllerParameterType.Bool:
                        _anim.SetBool(_hashes[i], v[i] > 0.5f); break;
                }
            }
            int layers = Mathf.Min(v.Length - paramCount, _anim.layerCount - 1);
            for (int l = 0; l < layers; l++) _anim.SetLayerWeight(l + 1, v[paramCount + l]);
        }

        /// <summary>
        /// Animator du corps du joueur : on prend un Animator humanoïde avec un
        /// controller (ignore ceux des armes, accessoires, etc.).
        /// </summary>
        public static Animator FindBodyAnimator(GameObject player)
        {
            if (player == null) return null;
            Animator best = null;
            int bestParams = -1;
            foreach (var a in player.GetComponentsInChildren<Animator>(true))
            {
                if (a == null || a.runtimeAnimatorController == null) continue;
                int score = a.parameterCount + (a.isHuman ? 1000 : 0);
                if (score > bestParams) { best = a; bestParams = score; }
            }
            return best;
        }
    }
}
