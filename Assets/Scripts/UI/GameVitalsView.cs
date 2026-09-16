using MotionControllers.Core;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    public sealed class GameVitalsView : VisualElement
    {
        private readonly Label heading=new Label();
        private readonly ProgressBar[] bars={new ProgressBar(),new ProgressBar()};
        public GameVitalsView(){AddToClassList("game-vitals");Add(bars[0]);Add(heading);Add(bars[1]);}
        public void Refresh(IGameVitals source)
        {
            var names=source.VitalsNames;var values=source.VitalsValues;if(values==null || names.Length<2)return;
            heading.text=source.VitalsHeading;
            for(int i=0;i<2;i++){bars[i].highValue=source.VitalsMaximum;bars[i].value=values[i];bars[i].title=names[i]+"  ·  "+values[i].ToString("0");}
        }
    }
}
