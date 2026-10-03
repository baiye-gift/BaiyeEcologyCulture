using KSerialization;
using UnityEngine;

namespace Baiye.EcologyCulture
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class CultureOutput : KMonoBehaviour,ISim200ms,ISaveLoadable
    {
        [Serialize] private bool productsRequested;
        [Serialize] private bool upgraded;
        private Storage products,samples;
        private static readonly CellOffset[] OutputOffsets={new CellOffset(-1,0),new CellOffset(2,0),new CellOffset(0,0),new CellOffset(-2,0),new CellOffset(3,0)};
        public bool Blocked { get; private set; }
        public bool HasStock=>products!=null&&(products.ExactMassStored()>1e-7||samples.ExactMassStored()>1e-7);
        protected override void OnSpawn()
        {
            base.OnSpawn();var stores=GetComponents<Storage>();products=stores[4];samples=stores[7];
            // One upgrade flush includes old small remnants and the reported
            // 48 kg backlog; no periodic micro-ejection afterward.
            if(!upgraded){productsRequested|=products.ExactMassStored()>1e-7;upgraded=true;}TryOutput();
        }
        public void Request(){productsRequested=true;TryOutput();}
        public void Committed(CultureStage before,CultureDelta delta)
        {productsRequested|=CultureOutputPolicy.FlushAfter(before,delta);TryOutput();}
        public void Sim200ms(float dt)=>TryOutput();
        private void TryOutput()
        {
            if(products==null)return;
            bool p=CultureOutputPolicy.ProductsDue(products.ExactMassStored(),productsRequested);
            bool s=CultureOutputPolicy.SamplesDue(samples.ExactMassStored());
            Blocked=false;if(!p&&!s){if(products.ExactMassStored()<=1e-7)productsRequested=false;return;}
            int cell=OutputCell();if(cell==Grid.InvalidCell){Blocked=true;return;}
            var position=Grid.CellToPosCCC(cell,Grid.SceneLayer.Ore);
            if(p){products.DropAll(position,do_disease_transfer:false);products.Trigger((int)GameHashes.OnStorageChange,null);productsRequested=false;}
            if(s){samples.DropAll(position,do_disease_transfer:false);samples.Trigger((int)GameHashes.OnStorageChange,null);}
        }
        private int OutputCell()
        {
            int origin=Grid.PosToCell(this);
            foreach(var offset in OutputOffsets)
            {
                int cell=Grid.OffsetCell(origin,offset);
                if(Grid.IsValidCell(cell)&&!Grid.Solid[cell]&&Grid.WorldIdx[cell]==Grid.WorldIdx[origin])return cell;
            }
            return Grid.InvalidCell;
        }
    }
}
