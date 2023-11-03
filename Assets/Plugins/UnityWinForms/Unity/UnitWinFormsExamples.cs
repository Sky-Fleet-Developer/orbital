using UnityEngine;
using UnityWinForms.Examples;

namespace UnityWinForms.Unity
{
    public class UnitWinFormsExamples : MonoBehaviour
    {
        public static Material s_chartGradient;
        public Material ChartGradient;
        
        private void Start()
        {
            s_chartGradient = ChartGradient;
            
            var form = new FormExamples();

            form.Show();
        }
    }
}