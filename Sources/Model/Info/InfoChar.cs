using TuTien.Model.Template;

namespace TuTien.Model.Info
{
    public class InfoChar
    {
        public long Hp { get; set; }
        public long Mp { get; set; }
        public long HpFull { get; set; }
        public long MpFull { get; set; }
        public long DamageFull { get; set; }
        public long DefenceFull { get; set; }
        public decimal CritFull { get; set; }
        public decimal DodgeFull { get; set; }

        public decimal OriginalKhiHuyet { get; set; }
        public decimal OriginalChanLuc { get; set; }
        public decimal OriginalPhapLuc { get; set; }
        public decimal OriginalLinhGiac { get; set; }
        public decimal OriginalSatCo { get; set; }

        public decimal KhiHuyetFromItem { get; set; }
        public decimal ChanLucFromItem { get; set; }
        public decimal PhapLucFromItem { get; set; }
        public decimal LinhGiacFromItem { get; set; }
        public decimal SatCoFromItem { get; set; }
        public decimal KhiHuyetFromPoint { get; set; }
        public decimal ChanLucFromPoint { get; set; }
        public decimal PhapLucFromPoint { get; set; }
        public decimal LinhGiacFromPoint { get; set; }
        public decimal SatCoFromPoint { get; set; }
        public decimal KhiHuyetFromEffect { get; set; }
        public decimal ChanLucFromEffect { get; set; }
        public decimal PhapLucFromEffect { get; set; }
        public decimal LinhGiacFromEffect { get; set; }
        public decimal SatCoFromEffect { get; set; }

        public decimal KhiHuyetFull { get; private set; }
        public decimal ChanLucFull { get; private set; }
        public decimal PhapLucFull { get; private set; }
        public decimal LinhGiacFull { get; private set; }
        public decimal SatCoFull { get; private set; }

        public InfoChar()
        {
        }

        public void SetUpInfo(RaceTemplate race)
        {
            KhiHuyetFull = (OriginalKhiHuyet + KhiHuyetFromPoint) * (100m + race.KhiHuyetPercent) / 100m + KhiHuyetFromItem + KhiHuyetFromEffect;
            ChanLucFull = (OriginalChanLuc + ChanLucFromPoint) * (100m + race.ChanLucPercent) / 100m + ChanLucFromItem + ChanLucFromEffect;
            PhapLucFull = (OriginalPhapLuc + PhapLucFromPoint) * (100m + race.PhapLucPercent) / 100m + PhapLucFromItem + PhapLucFromEffect;
            LinhGiacFull = (OriginalLinhGiac + LinhGiacFromPoint) * (100m + race.LinhGiacPercent) / 100m + LinhGiacFromItem + LinhGiacFromEffect;
            SatCoFull = (OriginalSatCo + SatCoFromPoint) * (100m + race.SatCoPercent) / 100m + SatCoFromItem + SatCoFromEffect;
        }

        public InfoChar Clone()
        {
            return (InfoChar)MemberwiseClone();
        }
    }
}
