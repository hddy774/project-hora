// 원본 게임(legacy/original-app.js)의 시뮬레이션을 시드 고정으로 실행해 피드백 문서(docs/01-feedback.md)의 수치를 재현한다.
// 원본 코드는 수정하지 않고, 읽어 온 문자열에 측정용 훅만 끼워 넣어 node:vm 안에서 실행한다.
// 실행: node tools/analyze-legacy.mjs [--ticks 30000] [--seeds 1,2,3]

import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { performance } from 'node:perf_hooks';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const LEGACY_FILE = path.join(HERE, '..', 'legacy', 'original-app.js');
const UI_BATCH_TICKS = 5; // 원본 UI의 기본 배속(16ms마다 5틱)과 같은 단위로 색인을 다시 만든다.
const HIGH_RANKS = new Set(['종주', '대장로', '장로']);

/** 명령줄 인자를 읽는다. */
function readOptions(argv) {
    const options = { ticks: 30000, seeds: [1, 2, 3] };
    for (let index = 0; index < argv.length; index += 2) {
        if (argv[index] === '--ticks') options.ticks = Number(argv[index + 1]);
        if (argv[index] === '--seeds') options.seeds = argv[index + 1].split(',').map(Number);
    }
    return options;
}

/** 측정 전용 시드 난수(sfc32). 원본의 Math.random을 대체해 실행을 재현 가능하게 만든다. */
function createSeededRandom(seed) {
    let a = 0x9e3779b9;
    let b = 0x243f6a88;
    let c = 0xb7e15162;
    let d = seed >>> 0;
    const next = () => {
        a >>>= 0; b >>>= 0; c >>>= 0; d >>>= 0;
        const t = (a + b + d) >>> 0;
        d = (d + 1) >>> 0;
        a = b ^ (b >>> 9);
        b = (c + (c << 3)) >>> 0;
        c = ((c << 21) | (c >>> 11)) >>> 0;
        c = (c + t) >>> 0;
        return t / 4294967296;
    };
    for (let i = 0; i < 16; i += 1) next();
    return next;
}

/** 원본 문자열에서 anchor를 정확히 한 번 찾아 바꾼다. 원본이 바뀌어 훅이 빠지는 일을 막는다. */
function replaceOnce(source, anchor, replacement) {
    const first = source.indexOf(anchor);
    if (first < 0 || source.indexOf(anchor, first + 1) >= 0) {
        throw new Error('측정 훅 위치를 하나로 특정할 수 없음: ' + anchor);
    }
    return source.replace(anchor, replacement);
}

/** 원본 소스를 vm에서 실행할 수 있는 스크립트로 바꾸고 측정 훅을 넣는다. */
function instrumentLegacySource(text) {
    let source = text.replace(/^import React[^\n]*\n/, '');
    source = replaceOnce(source, 'export default function App', 'function App');
    source = replaceOnce(source, 'p=cl(p*riMult*cvPenalty,.005,.995);',
        'p=cl(p*riMult*cvPenalty,.005,.995);__hook.breakthrough(c,p);');
    source = replaceOnce(source, 'function fight(lo,wi,tick,W,fatal){',
        'function fight(lo,wi,tick,W,fatal){__hook.fight(lo,wi,fatal);');
    source = replaceOnce(source, 'rg.pts=cl((rg.pts||0)+(wi.si===sid1?1:-1),-10,10);',
        '__hook.warPoint(wi,sid1);rg.pts=cl((rg.pts||0)+(wi.si===sid1?1:-1),-10,10);');
    source = replaceOnce(source, 'a.si=s.id;a.sr=rk;return true;',
        'a.si=s.id;a.sr=rk;__hook.slot(a,rk);return true;');
    source = replaceOnce(source, 'function realmDropS(c,tick,lg){',
        'function realmDropS(c,tick,lg){__hook.minorDrop(c);');
    // 최상위 const는 vm 전역 객체에 노출되지 않으므로 측정에 필요한 표만 꺼내 둔다.
    source += '\nthis.__tables={RL:RL,MS:MS,SR:SR};this.__getHistory=function(){return HD;};\n';
    return source;
}

/** 측정 카운터를 만든다. */
function createCounters() {
    return {
        attempts: 0, saturated: 0, probabilitySum: 0,
        warPoints: 0, warPointsToFirstSect: 0, warPointMismatch: 0,
        lastFightWinner: null, newbornHighRank: 0, minorDrops: 0,
        logByType: {}, maxInjury: 0,
    };
}

/** 원본 함수를 감싸는 훅 객체. */
function createHook(counters) {
    return {
        breakthrough(agent, probability) {
            counters.attempts += 1;
            counters.probabilitySum += probability;
            if (probability >= 0.99) counters.saturated += 1;
        },
        fight(loser, winner) {
            counters.lastFightWinner = winner;
            counters.maxInjury = Math.max(counters.maxInjury, (loser.inj || 0) + 1);
        },
        warPoint(pointWinner, firstSectId) {
            counters.warPoints += 1;
            if (pointWinner.si === firstSectId) counters.warPointsToFirstSect += 1;
            if (counters.lastFightWinner && counters.lastFightWinner.id !== pointWinner.id) {
                counters.warPointMismatch += 1;
            }
        },
        slot(agent, rank) {
            if (agent.age <= 16 && HIGH_RANKS.has(rank)) counters.newbornHighRank += 1;
        },
        minorDrop() {
            counters.minorDrops += 1;
        },
    };
}

/** 로그 배열의 push/unshift를 감싸 유형별 누적 건수를 센다(원본 로그는 잘려 나가므로). */
function countLogEvents(log, counters) {
    const record = (entries) => {
        for (const entry of entries) counters.logByType[entry.tp] = (counters.logByType[entry.tp] || 0) + 1;
    };
    log.push = function push(...entries) {
        record(entries);
        return Array.prototype.push.apply(this, entries);
    };
    log.unshift = function unshift(...entries) {
        record(entries);
        return Array.prototype.unshift.apply(this, entries);
    };
}

/** 현재 화면 스냅숏 로그가 최신순인지(틱이 증가하지 않는지) 검사한다. */
function measureLogOrder(snapshotLog) {
    let inversions = 0;
    for (let index = 1; index < snapshotLog.length; index += 1) {
        if (snapshotLog[index].tick > snapshotLog[index - 1].tick) inversions += 1;
    }
    const types = {};
    for (const entry of snapshotLog.slice(0, 120)) types[entry.tp] = (types[entry.tp] || 0) + 1;
    return { inversions, visibleTypes: types };
}

/** 살아 있는 수선자의 전투력 구성에서 재능·경지 항목 비중 평균을 구한다. */
function measurePowerShare(context, world) {
    let talent = 0;
    let realm = 0;
    for (const agent of world.ag) {
        const breakdown = context.cpb(agent, world.sc);
        talent += breakdown.tf / breakdown.tot;
        realm += breakdown.rf / breakdown.tot;
    }
    return { talentShare: talent / world.ag.length, realmShare: realm / world.ag.length };
}

/** 종주 표시값(fd)과 실제 명부의 종주가 일치하는지 센다. */
function measureLeaderMismatch(world) {
    return world.sc.filter((sect) => sect.ro['종주'].length > 0 && sect.fd !== sect.ro['종주'][0]).length;
}

/** 직위 재편 직후 명부에 없는데 직위(sr)를 가진 구성원 수를 센다. */
function measureStaleRanks(world) {
    let stale = 0;
    for (const sect of world.sc) {
        const listed = new Set(Object.values(sect.ro).flat());
        for (const agent of world.ag) {
            if (agent.si === sect.id && agent.sr && !listed.has(agent.id)) stale += 1;
        }
    }
    return stale;
}

/** 시드 하나로 원본 세계를 진행하며 지표를 모은다. */
function runSeed(source, seed, ticks) {
    const counters = createCounters();
    const seededMath = Object.create(Math);
    seededMath.random = createSeededRandom(seed);
    const context = vm.createContext({ Math: seededMath, React: {}, __hook: createHook(counters), console });
    vm.runInContext(source, context);
    const world = context.cw();
    countLogEvents(world.lg, counters);
    const realmCount = context.__tables.RL.length;
    const firstReach = new Array(realmCount).fill(null);
    const borderOwnersAfterWars = [];
    let staleRanks = 0;
    let index = context.bIdx(world);
    const startedAt = performance.now();
    for (let tick = 1; tick <= ticks; tick += 1) {
        context.simTick(world, tick, index);
        if (tick % UI_BATCH_TICKS === 0) index = context.bIdx(world);
        if (tick % 500 === 0) staleRanks = Math.max(staleRanks, measureStaleRanks(world));
        if (tick % 11000 === 1000) {
            borderOwnersAfterWars.push(world.rg.filter((region) => region.tp === '접경지').map((region) => region.ctrl));
        }
        for (const agent of world.ag) {
            if (firstReach[agent.ri] === null) firstReach[agent.ri] = tick;
        }
    }
    const elapsedMs = performance.now() - startedAt;
    const history = Object.values(context.__getHistory());
    return {
        seed,
        msPerTick: elapsedMs / ticks,
        realmDistribution: Array.from({ length: realmCount }, (_, realm) =>
            world.ag.filter((agent) => agent.ri === realm).length),
        firstReach,
        breakthroughAttempts: counters.attempts,
        saturatedShare: counters.saturated / Math.max(1, counters.attempts),
        meanProbability: counters.probabilitySum / Math.max(1, counters.attempts),
        warPoints: counters.warPoints,
        warPointsToFirstSect: counters.warPointsToFirstSect,
        warPointMismatch: counters.warPointMismatch,
        borderOwnersAfterWars,
        newbornHighRank: counters.newbornHighRank,
        staleRanks,
        leaderMismatch: measureLeaderMismatch(world),
        minorDrops: counters.minorDrops,
        maxInjury: counters.maxInjury,
        minMaxHp: Math.min(...world.ag.map((agent) => context.mHp(agent))),
        deaths: { ...world.ds },
        logByType: counters.logByType,
        logOrder: measureLogOrder(context.snap(world).lg),
        historyBadStage: history.filter((entry) => entry.bMi > 3).length,
        historyCount: history.length,
        maxReincarnation: Math.max(...world.ag.map((agent) => agent.rc)),
        ...measurePowerShare(context, world),
    };
}

const options = readOptions(process.argv.slice(2));
const source = instrumentLegacySource(fs.readFileSync(LEGACY_FILE, 'utf8'));
const results = options.seeds.map((seed) => runSeed(source, seed, options.ticks));
console.log(JSON.stringify({ ticks: options.ticks, results }, null, 2));
