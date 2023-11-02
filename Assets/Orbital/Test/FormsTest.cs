using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using UnityEngine;
using UnityWinForms.Examples;

namespace Orbital.Test
{
    public class FormsTest : MonoBehaviour
    {
        [Button]
        private void Start()
        {
            FormExamples form = new FormExamples();
            form.Show();
        }
    }
}