using Ara3D;
using Orbital.Core;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Orbital.View
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(IStaticBody))]
    public class StaticTrajectoryView : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private Mesh mesh;
        private IStaticBody _body;
        private Transform _viewTransform;
        private const string ViewName = "trajectory_view";
        private void Awake()
        {
            if (!Application.isPlaying)
            {
                GetComponentInParent<World>().Load();
            }
            _body = GetComponent<IStaticBody>();
        }

        private void OnEnable()
        {
            int searchIdx = transform.GetSiblingIndex() + 1;
            if (searchIdx < transform.parent.childCount)
            {
                _viewTransform = transform.parent.GetChild(searchIdx);
            }
            if (_viewTransform == null || _viewTransform.name != ViewName)
            {
                GameObject viewGameObject = new GameObject(ViewName, typeof(MeshFilter), typeof(MeshRenderer));
                viewGameObject.hideFlags = HideFlags.DontSave;
                _viewTransform = viewGameObject.transform;
                _viewTransform.SetParent(transform.parent);
                _viewTransform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            }

            _viewTransform.GetComponent<MeshFilter>().mesh = mesh;
            _viewTransform.GetComponent<MeshRenderer>().material = material;
        }

        private void Update()
        {
            #if UNITY_EDITOR
            if(_body == null) return;
            #endif
            Update(4.456328E-09F);
        }

        private void Update(float scale)
        {
            Vector3 fwd = _body.Trajectory.RotationMatrix * DVector3.forward;
            Vector3 up = _body.Trajectory.RotationMatrix * DVector3.up;
            _viewTransform.localPosition = ((Vector3) (_body.LocalPosition) - fwd * (float) _body.Trajectory.SemiMajorAxis) * scale;
            _viewTransform.rotation = Quaternion.LookRotation(fwd, up);
            _viewTransform.localScale = new Vector3((float) _body.Trajectory.SemiMinorAxis * scale, 1,
                (float) _body.Trajectory.SemiMajorAxis * scale);
        }

#if UNITY_EDITOR
        [Button]
        private static void GenerateMesh(string meshName, string path, int accuracy)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            {
                Vector3[] arr = new Vector3[accuracy];
                for (int i = 0; i < accuracy; i++)
                {
                    float angle = ((float) i / accuracy) * Mathf.PI * 2;
                    arr[i] = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                }

                mesh.vertices = arr;
            }

            {
                int[] arr = new int[accuracy * 2];
                for (int i = 0; i < accuracy - 1; i++)
                {
                    arr[i * 2] = i;
                    arr[i * 2 + 1] = i + 1;
                }
                arr[accuracy * 2 - 2] = accuracy - 1;
                arr[accuracy * 2 - 1] = 0;
                mesh.SetIndices(arr, MeshTopology.Lines, 0);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.MarkModified();
            AssetDatabase.CreateAsset(mesh, $"{path}/{meshName}.asset");
        }
#endif
    }
}