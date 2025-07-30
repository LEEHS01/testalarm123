using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onthesys.ExeBuild
{
    [System.Serializable]
    public class BoardStateModel
    {
        /// <summary>
        /// 관측 시각. yyyyMMddHHmmss 형식의 문자열
        /// </summary>
        public string obsdt;
        
        /// <summary>
        /// 보드 번호
        /// </summary>
        public int boardidx;
        
        /// <summary>
        /// 관측소 번호
        /// </summary>
        public int obsidx;  

        /// <summary>
        /// 2자리의 보드 상태 값. 일련 번호를 통해 해석
        /// 보드1 (해양 생태 독성도 감시 모듈)
        /// - 00 : 정상
        /// - 03 : 교정중
        /// - 06 : 동작 불량
        /// 보드2 (다기능 PNF 모듈)
        /// - 00 : 정상
        /// - 01 : 오류
        /// </summary>
        public string stcd;

        /// <summary>
        /// 2자리의 통신 에러 상태. 일련 번호를 통해 해석
        /// </summary>
        public string brd_state;

        /// <summary>
        /// 온도
        /// </summary>
        public float? meas_temp;
        /// <summary>
        /// 산성도
        /// </summary>
        public float? meas_ph;
        /// <summary>
        /// OHM
        /// </summary>
        public float? meas_ohm;

    }
}
