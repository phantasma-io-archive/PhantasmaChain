using System;

namespace Phantasma.Cryptography
{
	internal static class GroupOperations
	{
        /*
		r = p + q
		*/

        internal static void ge_add(out GroupElementP1P1 r, ref GroupElementP3 p, ref GroupElementCached q)
        {
            FieldElement t0;

            /* qhasm: enter GroupElementadd */

            /* qhasm: fe X1 */

            /* qhasm: fe Y1 */

            /* qhasm: fe Z1 */

            /* qhasm: fe Z2 */

            /* qhasm: fe T1 */

            /* qhasm: fe ZZ */

            /* qhasm: fe YpX2 */

            /* qhasm: fe YmX2 */

            /* qhasm: fe T2d2 */

            /* qhasm: fe X3 */

            /* qhasm: fe Y3 */

            /* qhasm: fe Z3 */

            /* qhasm: fe T3 */

            /* qhasm: fe YpX1 */

            /* qhasm: fe YmX1 */

            /* qhasm: fe A */

            /* qhasm: fe B */

            /* qhasm: fe C */

            /* qhasm: fe D */

            /* qhasm: YpX1 = Y1+X1 */
            /* asm 1: fe_add(>YpX1=fe#1,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_add(>YpX1=r.X,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_add(out r.X, ref p.Y, ref p.X);

            /* qhasm: YmX1 = Y1-X1 */
            /* asm 1: fe_sub(>YmX1=fe#2,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_sub(>YmX1=r.Y,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_sub(out r.Y, ref p.Y, ref p.X);

            /* qhasm: A = YpX1*YpX2 */
            /* asm 1: fe_mul(>A=fe#3,<YpX1=fe#1,<YpX2=fe#15); */
            /* asm 2: fe_mul(>A=r.Z,<YpX1=r.X,<YpX2=q.YplusX); */
            FieldOperations.fe_mul(out r.Z, ref r.X, ref q.YplusX);

            /* qhasm: B = YmX1*YmX2 */
            /* asm 1: fe_mul(>B=fe#2,<YmX1=fe#2,<YmX2=fe#16); */
            /* asm 2: fe_mul(>B=r.Y,<YmX1=r.Y,<YmX2=q.YminusX); */
            FieldOperations.fe_mul(out r.Y, ref r.Y, ref q.YminusX);

            /* qhasm: C = T2d2*T1 */
            /* asm 1: fe_mul(>C=fe#4,<T2d2=fe#18,<T1=fe#14); */
            /* asm 2: fe_mul(>C=r.T,<T2d2=q.T2d,<T1=p.T); */
            FieldOperations.fe_mul(out r.T, ref q.T2d, ref p.T);

            /* qhasm: ZZ = Z1*Z2 */
            /* asm 1: fe_mul(>ZZ=fe#1,<Z1=fe#13,<Z2=fe#17); */
            /* asm 2: fe_mul(>ZZ=r.X,<Z1=p.Z,<Z2=q.Z); */
            FieldOperations.fe_mul(out r.X, ref p.Z, ref q.Z);

            /* qhasm: D = 2*ZZ */
            /* asm 1: fe_add(>D=fe#5,<ZZ=fe#1,<ZZ=fe#1); */
            /* asm 2: fe_add(>D=t0,<ZZ=r.X,<ZZ=r.X); */
            FieldOperations.fe_add(out t0, ref r.X, ref r.X);

            /* qhasm: X3 = A-B */
            /* asm 1: fe_sub(>X3=fe#1,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_sub(>X3=r.X,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_sub(out r.X, ref r.Z, ref r.Y);

            /* qhasm: Y3 = A+B */
            /* asm 1: fe_add(>Y3=fe#2,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_add(>Y3=r.Y,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_add(out r.Y, ref r.Z, ref r.Y);

            /* qhasm: Z3 = D+C */
            /* asm 1: fe_add(>Z3=fe#3,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_add(>Z3=r.Z,<D=t0,<C=r.T); */
            FieldOperations.fe_add(out r.Z, ref t0, ref r.T);

            /* qhasm: T3 = D-C */
            /* asm 1: fe_sub(>T3=fe#4,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_sub(>T3=r.T,<D=t0,<C=r.T); */
            FieldOperations.fe_sub(out r.T, ref t0, ref r.T);

            /* qhasm: return */
        }

        private static void slide(sbyte[] r, byte[] a)
        {
            int i;
            int b;
            int k;

            for (i = 0; i < 256; ++i)
                r[i] = (sbyte)(1 & (a[i >> 3] >> (i & 7)));

            for (i = 0; i < 256; ++i)
                if (r[i] != 0)
                {
                    for (b = 1; b <= 6 && i + b < 256; ++b)
                    {
                        if (r[i + b] != 0)
                        {
                            if (r[i] + (r[i + b] << b) <= 15)
                            {
                                r[i] += (sbyte)(r[i + b] << b); r[i + b] = 0;
                            }
                            else if (r[i] - (r[i + b] << b) >= -15)
                            {
                                r[i] -= (sbyte)(r[i + b] << b);
                                for (k = i + b; k < 256; ++k)
                                {
                                    if (r[k] == 0)
                                    {
                                        r[k] = 1;
                                        break;
                                    }
                                    r[k] = 0;
                                }
                            }
                            else
                                break;
                        }
                    }
                }

        }

        /*
		r = a * A + b * B
		where a = a[0]+256*a[1]+...+256^31 a[31].
		and b = b[0]+256*b[1]+...+256^31 b[31].
		B is the Ed25519 base point (x,4/5) with x positive.
		*/

        public static void ge_double_scalarmult_vartime(out GroupElementP2 r, byte[] a, ref GroupElementP3 A, byte[] b)
        {
            GroupElementPreComp[] Bi = LookupTables.Base2;
            // todo: Perhaps remove these allocations?
            sbyte[] aslide = new sbyte[256];
            sbyte[] bslide = new sbyte[256];
            GroupElementCached[] Ai = new GroupElementCached[8]; /* A,3A,5A,7A,9A,11A,13A,15A */
            GroupElementP1P1 t;
            GroupElementP3 u;
            GroupElementP3 A2;
            int i;

            slide(aslide, a);
            slide(bslide, b);

            ge_p3_to_cached(out Ai[0], ref A);
            ge_p3_dbl(out t, ref A); ge_p1p1_to_p3(out A2, ref t);
            ge_add(out t, ref A2, ref Ai[0]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[1], ref u);
            ge_add(out t, ref A2, ref Ai[1]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[2], ref u);
            ge_add(out t, ref A2, ref Ai[2]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[3], ref u);
            ge_add(out t, ref A2, ref Ai[3]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[4], ref u);
            ge_add(out t, ref A2, ref Ai[4]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[5], ref u);
            ge_add(out t, ref A2, ref Ai[5]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[6], ref u);
            ge_add(out t, ref A2, ref Ai[6]); ge_p1p1_to_p3(out u, ref t); ge_p3_to_cached(out Ai[7], ref u);

            ge_p2_0(out r);

            for (i = 255; i >= 0; --i)
            {
                if ((aslide[i] != 0) || (bslide[i] != 0)) break;
            }

            for (; i >= 0; --i)
            {
                ge_p2_dbl(out t, ref r);

                if (aslide[i] > 0)
                {
                    ge_p1p1_to_p3(out u, ref t);
                    ge_add(out t, ref u, ref Ai[aslide[i] / 2]);
                }
                else if (aslide[i] < 0)
                {
                    ge_p1p1_to_p3(out u, ref t);
                    ge_sub(out t, ref u, ref Ai[(-aslide[i]) / 2]);
                }

                if (bslide[i] > 0)
                {
                    ge_p1p1_to_p3(out u, ref t);
                    ge_madd(out t, ref u, ref Bi[bslide[i] / 2]);
                }
                else if (bslide[i] < 0)
                {
                    ge_p1p1_to_p3(out u, ref t);
                    ge_msub(out t, ref u, ref Bi[(-bslide[i]) / 2]);
                }

                ge_p1p1_to_p2(out r, ref t);
            }
        }

        public static int ge_frombytes_negate_vartime(out GroupElementP3 h, byte[] data, int offset)
        {
            FieldElement u;
            FieldElement v;
            FieldElement v3;
            FieldElement vxx;
            FieldElement check;

            FieldOperations.fe_frombytes(out h.Y, data, offset);
            FieldOperations.fe_1(out h.Z);
            FieldOperations.fe_sq(out u, ref h.Y);
            FieldOperations.fe_mul(out v, ref u, ref LookupTables.d);
            FieldOperations.fe_sub(out u, ref u, ref h.Z);       /* u = y^2-1 */
            FieldOperations.fe_add(out v, ref v, ref h.Z);       /* v = dy^2+1 */

            FieldOperations.fe_sq(out v3, ref v);
            FieldOperations.fe_mul(out v3, ref v3, ref v);        /* v3 = v^3 */
            FieldOperations.fe_sq(out h.X, ref v3);
            FieldOperations.fe_mul(out h.X, ref h.X, ref v);
            FieldOperations.fe_mul(out h.X, ref h.X, ref u);    /* x = uv^7 */

            FieldOperations.fe_pow22523(out h.X, ref h.X); /* x = (uv^7)^((q-5)/8) */
            FieldOperations.fe_mul(out h.X, ref h.X, ref v3);
            FieldOperations.fe_mul(out h.X, ref h.X, ref u);    /* x = uv^3(uv^7)^((q-5)/8) */

            FieldOperations.fe_sq(out vxx, ref h.X);
            FieldOperations.fe_mul(out vxx, ref vxx, ref v);
            FieldOperations.fe_sub(out check, ref vxx, ref u);    /* vx^2-u */
            if (FieldOperations.fe_isnonzero(ref check) != 0)
            {
                FieldOperations.fe_add(out check, ref vxx, ref u);  /* vx^2+u */
                if (FieldOperations.fe_isnonzero(ref check) != 0)
                {
                    h = default(GroupElementP3);
                    return -1;
                }
                FieldOperations.fe_mul(out h.X, ref h.X, ref LookupTables.sqrtm1);
            }

            if (FieldOperations.fe_isnegative(ref h.X) == (data[offset + 31] >> 7))
                FieldOperations.fe_neg(out h.X, ref h.X);

            FieldOperations.fe_mul(out h.T, ref h.X, ref h.Y);
            return 0;
        }

        /*
		r = p + q
		*/
        public static void ge_madd(out GroupElementP1P1 r, ref GroupElementP3 p, ref GroupElementPreComp q)
        {
            FieldElement t0;

            /* qhasm: enter ge_madd */

            /* qhasm: fe X1 */

            /* qhasm: fe Y1 */

            /* qhasm: fe Z1 */

            /* qhasm: fe T1 */

            /* qhasm: fe ypx2 */

            /* qhasm: fe ymx2 */

            /* qhasm: fe xy2d2 */

            /* qhasm: fe X3 */

            /* qhasm: fe Y3 */

            /* qhasm: fe Z3 */

            /* qhasm: fe T3 */

            /* qhasm: fe YpX1 */

            /* qhasm: fe YmX1 */

            /* qhasm: fe A */

            /* qhasm: fe B */

            /* qhasm: fe C */

            /* qhasm: fe D */

            /* qhasm: YpX1 = Y1+X1 */
            /* asm 1: fe_add(>YpX1=fe#1,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_add(>YpX1=r.X,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_add(out r.X, ref p.Y, ref p.X);

            /* qhasm: YmX1 = Y1-X1 */
            /* asm 1: fe_sub(>YmX1=fe#2,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_sub(>YmX1=r.Y,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_sub(out r.Y, ref p.Y, ref p.X);

            /* qhasm: A = YpX1*ypx2 */
            /* asm 1: fe_mul(>A=fe#3,<YpX1=fe#1,<ypx2=fe#15); */
            /* asm 2: fe_mul(>A=r.Z,<YpX1=r.X,<ypx2=q.yplusx); */
            FieldOperations.fe_mul(out r.Z, ref r.X, ref q.yplusx);

            /* qhasm: B = YmX1*ymx2 */
            /* asm 1: fe_mul(>B=fe#2,<YmX1=fe#2,<ymx2=fe#16); */
            /* asm 2: fe_mul(>B=r.Y,<YmX1=r.Y,<ymx2=q.yminusx); */
            FieldOperations.fe_mul(out r.Y, ref r.Y, ref q.yminusx);

            /* qhasm: C = xy2d2*T1 */
            /* asm 1: fe_mul(>C=fe#4,<xy2d2=fe#17,<T1=fe#14); */
            /* asm 2: fe_mul(>C=r.T,<xy2d2=q.xy2d,<T1=p.T); */
            FieldOperations.fe_mul(out r.T, ref q.xy2d, ref p.T);

            /* qhasm: D = 2*Z1 */
            /* asm 1: fe_add(>D=fe#5,<Z1=fe#13,<Z1=fe#13); */
            /* asm 2: fe_add(>D=t0,<Z1=p.Z,<Z1=p.Z); */
            FieldOperations.fe_add(out t0, ref p.Z, ref p.Z);

            /* qhasm: X3 = A-B */
            /* asm 1: fe_sub(>X3=fe#1,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_sub(>X3=r.X,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_sub(out r.X, ref r.Z, ref r.Y);

            /* qhasm: Y3 = A+B */
            /* asm 1: fe_add(>Y3=fe#2,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_add(>Y3=r.Y,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_add(out r.Y, ref r.Z, ref r.Y);

            /* qhasm: Z3 = D+C */
            /* asm 1: fe_add(>Z3=fe#3,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_add(>Z3=r.Z,<D=t0,<C=r.T); */
            FieldOperations.fe_add(out r.Z, ref t0, ref r.T);

            /* qhasm: T3 = D-C */
            /* asm 1: fe_sub(>T3=fe#4,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_sub(>T3=r.T,<D=t0,<C=r.T); */
            FieldOperations.fe_sub(out r.T, ref t0, ref r.T);

            /* qhasm: return */

        }

        /*
		r = p - q
		*/
        public static void ge_msub(out GroupElementP1P1 r, ref GroupElementP3 p, ref GroupElementPreComp q)
        {
            FieldElement t0;

            /* qhasm: enter ge_msub */

            /* qhasm: fe X1 */

            /* qhasm: fe Y1 */

            /* qhasm: fe Z1 */

            /* qhasm: fe T1 */

            /* qhasm: fe ypx2 */

            /* qhasm: fe ymx2 */

            /* qhasm: fe xy2d2 */

            /* qhasm: fe X3 */

            /* qhasm: fe Y3 */

            /* qhasm: fe Z3 */

            /* qhasm: fe T3 */

            /* qhasm: fe YpX1 */

            /* qhasm: fe YmX1 */

            /* qhasm: fe A */

            /* qhasm: fe B */

            /* qhasm: fe C */

            /* qhasm: fe D */

            /* qhasm: YpX1 = Y1+X1 */
            /* asm 1: fe_add(>YpX1=fe#1,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_add(>YpX1=r.X,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_add(out r.X, ref p.Y, ref p.X);

            /* qhasm: YmX1 = Y1-X1 */
            /* asm 1: fe_sub(>YmX1=fe#2,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_sub(>YmX1=r.Y,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_sub(out r.Y, ref p.Y, ref p.X);

            /* qhasm: A = YpX1*ymx2 */
            /* asm 1: fe_mul(>A=fe#3,<YpX1=fe#1,<ymx2=fe#16); */
            /* asm 2: fe_mul(>A=r.Z,<YpX1=r.X,<ymx2=q.yminusx); */
            FieldOperations.fe_mul(out r.Z, ref r.X, ref q.yminusx);

            /* qhasm: B = YmX1*ypx2 */
            /* asm 1: fe_mul(>B=fe#2,<YmX1=fe#2,<ypx2=fe#15); */
            /* asm 2: fe_mul(>B=r.Y,<YmX1=r.Y,<ypx2=q.yplusx); */
            FieldOperations.fe_mul(out r.Y, ref r.Y, ref q.yplusx);

            /* qhasm: C = xy2d2*T1 */
            /* asm 1: fe_mul(>C=fe#4,<xy2d2=fe#17,<T1=fe#14); */
            /* asm 2: fe_mul(>C=r.T,<xy2d2=q.xy2d,<T1=p.T); */
            FieldOperations.fe_mul(out r.T, ref q.xy2d, ref p.T);

            /* qhasm: D = 2*Z1 */
            /* asm 1: fe_add(>D=fe#5,<Z1=fe#13,<Z1=fe#13); */
            /* asm 2: fe_add(>D=t0,<Z1=p.Z,<Z1=p.Z); */
            FieldOperations.fe_add(out t0, ref p.Z, ref p.Z);

            /* qhasm: X3 = A-B */
            /* asm 1: fe_sub(>X3=fe#1,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_sub(>X3=r.X,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_sub(out r.X, ref r.Z, ref r.Y);

            /* qhasm: Y3 = A+B */
            /* asm 1: fe_add(>Y3=fe#2,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_add(>Y3=r.Y,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_add(out r.Y, ref r.Z, ref r.Y);

            /* qhasm: Z3 = D-C */
            /* asm 1: fe_sub(>Z3=fe#3,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_sub(>Z3=r.Z,<D=t0,<C=r.T); */
            FieldOperations.fe_sub(out r.Z, ref t0, ref r.T);

            /* qhasm: T3 = D+C */
            /* asm 1: fe_add(>T3=fe#4,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_add(>T3=r.T,<D=t0,<C=r.T); */
            FieldOperations.fe_add(out r.T, ref t0, ref r.T);

            /* qhasm: return */

        }

        /*
		r = p
		*/
        public static void ge_p1p1_to_p2(out GroupElementP2 r, ref GroupElementP1P1 p)
        {
            FieldOperations.fe_mul(out r.X, ref p.X, ref p.T);
            FieldOperations.fe_mul(out r.Y, ref p.Y, ref p.Z);
            FieldOperations.fe_mul(out r.Z, ref p.Z, ref p.T);
        }

        /*
		r = p
		*/
        public static void ge_p1p1_to_p3(out GroupElementP3 r, ref GroupElementP1P1 p)
        {
            FieldOperations.fe_mul(out r.X, ref p.X, ref p.T);
            FieldOperations.fe_mul(out r.Y, ref p.Y, ref p.Z);
            FieldOperations.fe_mul(out r.Z, ref p.Z, ref p.T);
            FieldOperations.fe_mul(out r.T, ref p.X, ref p.Y);
        }

        public static void ge_p2_0(out GroupElementP2 h)
        {
            FieldOperations.fe_0(out h.X);
            FieldOperations.fe_1(out h.Y);
            FieldOperations.fe_1(out h.Z);
        }

        /*
		r = 2 * p
		*/

        public static void ge_p2_dbl(out GroupElementP1P1 r, ref GroupElementP2 p)
        {
            FieldElement t0;

            /* qhasm: enter ge_p2_dbl */

            /* qhasm: fe X1 */

            /* qhasm: fe Y1 */

            /* qhasm: fe Z1 */

            /* qhasm: fe A */

            /* qhasm: fe AA */

            /* qhasm: fe XX */

            /* qhasm: fe YY */

            /* qhasm: fe B */

            /* qhasm: fe X3 */

            /* qhasm: fe Y3 */

            /* qhasm: fe Z3 */

            /* qhasm: fe T3 */

            /* qhasm: XX=X1^2 */
            /* asm 1: fe_sq(>XX=fe#1,<X1=fe#11); */
            /* asm 2: fe_sq(>XX=r.X,<X1=p.X); */
            FieldOperations.fe_sq(out r.X, ref p.X);

            /* qhasm: YY=Y1^2 */
            /* asm 1: fe_sq(>YY=fe#3,<Y1=fe#12); */
            /* asm 2: fe_sq(>YY=r.Z,<Y1=p.Y); */
            FieldOperations.fe_sq(out r.Z, ref p.Y);

            /* qhasm: B=2*Z1^2 */
            /* asm 1: fe_sq2(>B=fe#4,<Z1=fe#13); */
            /* asm 2: fe_sq2(>B=r.T,<Z1=p.Z); */
            FieldOperations.fe_sq2(out r.T, ref p.Z);

            /* qhasm: A=X1+Y1 */
            /* asm 1: fe_add(>A=fe#2,<X1=fe#11,<Y1=fe#12); */
            /* asm 2: fe_add(>A=r.Y,<X1=p.X,<Y1=p.Y); */
            FieldOperations.fe_add(out r.Y, ref p.X, ref p.Y);

            /* qhasm: AA=A^2 */
            /* asm 1: fe_sq(>AA=fe#5,<A=fe#2); */
            /* asm 2: fe_sq(>AA=t0,<A=r.Y); */
            FieldOperations.fe_sq(out t0, ref r.Y);

            /* qhasm: Y3=YY+XX */
            /* asm 1: fe_add(>Y3=fe#2,<YY=fe#3,<XX=fe#1); */
            /* asm 2: fe_add(>Y3=r.Y,<YY=r.Z,<XX=r.X); */
            FieldOperations.fe_add(out r.Y, ref r.Z, ref r.X);

            /* qhasm: Z3=YY-XX */
            /* asm 1: fe_sub(>Z3=fe#3,<YY=fe#3,<XX=fe#1); */
            /* asm 2: fe_sub(>Z3=r.Z,<YY=r.Z,<XX=r.X); */
            FieldOperations.fe_sub(out r.Z, ref r.Z, ref r.X);

            /* qhasm: X3=AA-Y3 */
            /* asm 1: fe_sub(>X3=fe#1,<AA=fe#5,<Y3=fe#2); */
            /* asm 2: fe_sub(>X3=r.X,<AA=t0,<Y3=r.Y); */
            FieldOperations.fe_sub(out r.X, ref t0, ref r.Y);

            /* qhasm: T3=B-Z3 */
            /* asm 1: fe_sub(>T3=fe#4,<B=fe#4,<Z3=fe#3); */
            /* asm 2: fe_sub(>T3=r.T,<B=r.T,<Z3=r.Z); */
            FieldOperations.fe_sub(out r.T, ref r.T, ref r.Z);

            /* qhasm: return */

        }

        public static void ge_p3_0(out GroupElementP3 h)
        {
            FieldOperations.fe_0(out h.X);
            FieldOperations.fe_1(out h.Y);
            FieldOperations.fe_1(out h.Z);
            FieldOperations.fe_0(out h.T);
        }

        /*
		r = 2 * p
		*/
        public static void ge_p3_dbl(out GroupElementP1P1 r, ref GroupElementP3 p)
        {
            GroupElementP2 q;
            ge_p3_to_p2(out q, ref p);
            ge_p2_dbl(out r, ref q);
        }

        /*
		r = p
		*/
        public static void ge_p3_to_cached(out GroupElementCached r, ref GroupElementP3 p)
        {
            FieldOperations.fe_add(out r.YplusX, ref p.Y, ref p.X);
            FieldOperations.fe_sub(out r.YminusX, ref p.Y, ref p.X);
            r.Z = p.Z;
            FieldOperations.fe_mul(out r.T2d, ref p.T, ref LookupTables.d2);
        }

        /*
		r = p
		*/
        public static void ge_p3_to_p2(out GroupElementP2 r, ref GroupElementP3 p)
        {
            r.X = p.X;
            r.Y = p.Y;
            r.Z = p.Z;
        }

        public static void ge_p3_tobytes(byte[] s, int offset, ref GroupElementP3 h)
        {
            FieldElement recip;
            FieldElement x;
            FieldElement y;

            FieldOperations.fe_invert(out recip, ref h.Z);
            FieldOperations.fe_mul(out x, ref h.X, ref recip);
            FieldOperations.fe_mul(out y, ref h.Y, ref recip);
            FieldOperations.fe_tobytes(s, offset, ref y);
            s[offset + 31] ^= (byte)(FieldOperations.fe_isnegative(ref x) << 7);
        }

        public static void ge_precomp_0(out GroupElementPreComp h)
        {
            FieldOperations.fe_1(out h.yplusx);
            FieldOperations.fe_1(out h.yminusx);
            FieldOperations.fe_0(out h.xy2d);
        }

        static byte equal(byte b, byte c)
        {

            byte ub = b;
            byte uc = c;
            byte x = (byte)(ub ^ uc); /* 0: yes; 1..255: no */
            UInt32 y = x; /* 0: yes; 1..255: no */
            unchecked { y -= 1; } /* 4294967295: yes; 0..254: no */
            y >>= 31; /* 1: yes; 0: no */
            return (byte)y;
        }

        static byte negative(sbyte b)
        {
            ulong x = unchecked((ulong)(long)b); /* 18446744073709551361..18446744073709551615: yes; 0..255: no */
            x >>= 63; /* 1: yes; 0: no */
            return (byte)x;
        }

        static void cmov(ref GroupElementPreComp t, ref GroupElementPreComp u, byte b)
        {
            FieldOperations.fe_cmov(ref t.yplusx, ref u.yplusx, b);
            FieldOperations.fe_cmov(ref t.yminusx, ref u.yminusx, b);
            FieldOperations.fe_cmov(ref t.xy2d, ref u.xy2d, b);
        }

        static void select(out GroupElementPreComp t, int pos, sbyte b)
        {
            GroupElementPreComp minust;
            byte bnegative = negative(b);
            byte babs = (byte)(b - (((-bnegative) & b) << 1));

            ge_precomp_0(out t);
            var table = LookupTables.Base[pos];
            cmov(ref t, ref table[0], equal(babs, 1));
            cmov(ref t, ref table[1], equal(babs, 2));
            cmov(ref t, ref table[2], equal(babs, 3));
            cmov(ref t, ref table[3], equal(babs, 4));
            cmov(ref t, ref table[4], equal(babs, 5));
            cmov(ref t, ref table[5], equal(babs, 6));
            cmov(ref t, ref table[6], equal(babs, 7));
            cmov(ref t, ref table[7], equal(babs, 8));
            minust.yplusx = t.yminusx;
            minust.yminusx = t.yplusx;
            FieldOperations.fe_neg(out minust.xy2d, ref t.xy2d);
            cmov(ref t, ref minust, bnegative);
        }

        /*
        h = a * B
        where a = a[0]+256*a[1]+...+256^31 a[31]
        B is the Ed25519 base point (x,4/5) with x positive.

        Preconditions:
          a[31] <= 127
        */

        public static void ge_scalarmult_base(out GroupElementP3 h, byte[] a, int offset)
        {
            // todo: Perhaps remove this allocation
            sbyte[] e = new sbyte[64];
            sbyte carry;
            GroupElementP1P1 r;
            GroupElementP2 s;
            GroupElementPreComp t;
            int i;

            for (i = 0; i < 32; ++i)
            {
                e[2 * i + 0] = (sbyte)((a[offset + i] >> 0) & 15);
                e[2 * i + 1] = (sbyte)((a[offset + i] >> 4) & 15);
            }
            /* each e[i] is between 0 and 15 */
            /* e[63] is between 0 and 7 */

            carry = 0;
            for (i = 0; i < 63; ++i)
            {
                e[i] += carry;
                carry = (sbyte)(e[i] + 8);
                carry >>= 4;
                e[i] -= (sbyte)(carry << 4);
            }
            e[63] += carry;
            /* each e[i] is between -8 and 8 */

            ge_p3_0(out h);
            for (i = 1; i < 64; i += 2)
            {
                select(out t, i / 2, e[i]);
                ge_madd(out r, ref h, ref t); ge_p1p1_to_p3(out h, ref r);
            }

            ge_p3_dbl(out r, ref h); ge_p1p1_to_p2(out s, ref r);
            ge_p2_dbl(out r, ref s); ge_p1p1_to_p2(out s, ref r);
            ge_p2_dbl(out r, ref s); ge_p1p1_to_p2(out s, ref r);
            ge_p2_dbl(out r, ref s); ge_p1p1_to_p3(out h, ref r);

            for (i = 0; i < 64; i += 2)
            {
                select(out t, i / 2, e[i]);
                ge_madd(out r, ref h, ref t); ge_p1p1_to_p3(out h, ref r);
            }
        }

        /*
		r = p - q
		*/

        public static void ge_sub(out GroupElementP1P1 r, ref GroupElementP3 p, ref GroupElementCached q)
        {
            FieldElement t0;

            /* qhasm: enter ge_sub */

            /* qhasm: fe X1 */

            /* qhasm: fe Y1 */

            /* qhasm: fe Z1 */

            /* qhasm: fe Z2 */

            /* qhasm: fe T1 */

            /* qhasm: fe ZZ */

            /* qhasm: fe YpX2 */

            /* qhasm: fe YmX2 */

            /* qhasm: fe T2d2 */

            /* qhasm: fe X3 */

            /* qhasm: fe Y3 */

            /* qhasm: fe Z3 */

            /* qhasm: fe T3 */

            /* qhasm: fe YpX1 */

            /* qhasm: fe YmX1 */

            /* qhasm: fe A */

            /* qhasm: fe B */

            /* qhasm: fe C */

            /* qhasm: fe D */

            /* qhasm: YpX1 = Y1+X1 */
            /* asm 1: fe_add(>YpX1=fe#1,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_add(>YpX1=r.X,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_add(out r.X, ref p.Y, ref p.X);

            /* qhasm: YmX1 = Y1-X1 */
            /* asm 1: fe_sub(>YmX1=fe#2,<Y1=fe#12,<X1=fe#11); */
            /* asm 2: fe_sub(>YmX1=r.Y,<Y1=p.Y,<X1=p.X); */
            FieldOperations.fe_sub(out r.Y, ref p.Y, ref p.X);

            /* qhasm: A = YpX1*YmX2 */
            /* asm 1: fe_mul(>A=fe#3,<YpX1=fe#1,<YmX2=fe#16); */
            /* asm 2: fe_mul(>A=r.Z,<YpX1=r.X,<YmX2=q.YminusX); */
            FieldOperations.fe_mul(out r.Z, ref r.X, ref q.YminusX);

            /* qhasm: B = YmX1*YpX2 */
            /* asm 1: fe_mul(>B=fe#2,<YmX1=fe#2,<YpX2=fe#15); */
            /* asm 2: fe_mul(>B=r.Y,<YmX1=r.Y,<YpX2=q.YplusX); */
            FieldOperations.fe_mul(out r.Y, ref r.Y, ref q.YplusX);

            /* qhasm: C = T2d2*T1 */
            /* asm 1: fe_mul(>C=fe#4,<T2d2=fe#18,<T1=fe#14); */
            /* asm 2: fe_mul(>C=r.T,<T2d2=q.T2d,<T1=p.T); */
            FieldOperations.fe_mul(out r.T, ref q.T2d, ref p.T);

            /* qhasm: ZZ = Z1*Z2 */
            /* asm 1: fe_mul(>ZZ=fe#1,<Z1=fe#13,<Z2=fe#17); */
            /* asm 2: fe_mul(>ZZ=r.X,<Z1=p.Z,<Z2=q.Z); */
            FieldOperations.fe_mul(out r.X, ref p.Z, ref q.Z);

            /* qhasm: D = 2*ZZ */
            /* asm 1: fe_add(>D=fe#5,<ZZ=fe#1,<ZZ=fe#1); */
            /* asm 2: fe_add(>D=t0,<ZZ=r.X,<ZZ=r.X); */
            FieldOperations.fe_add(out t0, ref r.X, ref r.X);

            /* qhasm: X3 = A-B */
            /* asm 1: fe_sub(>X3=fe#1,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_sub(>X3=r.X,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_sub(out r.X, ref r.Z, ref r.Y);

            /* qhasm: Y3 = A+B */
            /* asm 1: fe_add(>Y3=fe#2,<A=fe#3,<B=fe#2); */
            /* asm 2: fe_add(>Y3=r.Y,<A=r.Z,<B=r.Y); */
            FieldOperations.fe_add(out r.Y, ref r.Z, ref r.Y);

            /* qhasm: Z3 = D-C */
            /* asm 1: fe_sub(>Z3=fe#3,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_sub(>Z3=r.Z,<D=t0,<C=r.T); */
            FieldOperations.fe_sub(out r.Z, ref t0, ref r.T);

            /* qhasm: T3 = D+C */
            /* asm 1: fe_add(>T3=fe#4,<D=fe#5,<C=fe#4); */
            /* asm 2: fe_add(>T3=r.T,<D=t0,<C=r.T); */
            FieldOperations.fe_add(out r.T, ref t0, ref r.T);

            /* qhasm: return */
        }


        public static void ge_tobytes(byte[] s, int offset, ref  GroupElementP2 h)
		{
			FieldElement recip;
			FieldElement x;
			FieldElement y;

			FieldOperations.fe_invert(out recip, ref h.Z);
			FieldOperations.fe_mul(out x, ref h.X, ref recip);
			FieldOperations.fe_mul(out y, ref h.Y, ref recip);
			FieldOperations.fe_tobytes(s, offset, ref y);
			s[offset + 31] ^= (byte)(FieldOperations.fe_isnegative(ref x) << 7);
		}
	}
}