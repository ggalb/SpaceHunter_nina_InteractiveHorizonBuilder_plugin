using System.ComponentModel.Composition;
using System.Windows;

namespace InteractiveHorizonBuilder {

    [Export(typeof(ResourceDictionary))]
    public partial class DockableTemplate : ResourceDictionary {

        public DockableTemplate() {
            InitializeComponent();
        }
    }
}
