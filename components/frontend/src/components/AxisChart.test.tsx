import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { AxisChart } from './AxisChart';
import { ThemeProvider } from '../context/ThemeProvider';
import type { AlignedData } from '../utils/chartData';
import type { AxisChartSeries } from './AxisChart';

// jsdom has no canvas — replace uPlot with a recording, constructible stub.
const instances = vi.hoisted(
    () =>
        [] as Array<{
            options: { series: Array<{ label?: string }>; bands?: unknown };
            data: unknown;
            setData: ReturnType<typeof vi.fn>;
            setSeries: ReturnType<typeof vi.fn>;
            destroy: ReturnType<typeof vi.fn>;
        }>,
);

vi.mock('uplot', () => {
    class UPlotStub {
        options: { series: Array<{ label?: string }>; bands?: unknown };
        data: unknown;
        setData = vi.fn();
        setSize = vi.fn();
        setSeries = vi.fn();
        destroy = vi.fn();

        constructor(options: { series: Array<{ label?: string }> }, data: unknown) {
            this.options = options;
            this.data = data;
            instances.push(this);
        }
    }
    return { default: UPlotStub };
});
vi.mock('uplot/dist/uPlot.min.css', () => ({}));

const SERIES: AxisChartSeries[] = [
    { label: 'ax', color: '#f00' },
    { label: 'ay', color: '#0f0' },
    { label: 'az', color: '#00f' },
];

const dataA: AlignedData = [
    [0, 1],
    [1, 2],
    [3, 4],
    [5, 6],
];
const dataB: AlignedData = [
    [0, 1, 2],
    [1, 2, 3],
    [3, 4, 5],
    [5, 6, 7],
];

function renderChart(data: AlignedData) {
    return render(
        <ThemeProvider>
            <AxisChart title="Accel" unit="m/s²" series={SERIES} data={data} />
        </ThemeProvider>,
    );
}

describe('AxisChart', () => {
    beforeEach(() => {
        instances.length = 0;
        localStorage.clear();
    });

    it('creates one uPlot instance with the three labeled series', () => {
        renderChart(dataA);

        expect(instances).toHaveLength(1);
        expect(instances[0]!.options.series.map((s) => s.label)).toEqual([
            't [s]',
            'ax',
            'ay',
            'az',
        ]);
    });

    it('feeds data changes through setData without recreating the instance', () => {
        const { rerender } = renderChart(dataA);

        rerender(
            <ThemeProvider>
                <AxisChart title="Accel" unit="m/s²" series={SERIES} data={dataB} />
            </ThemeProvider>,
        );

        expect(instances).toHaveLength(1);
        expect(instances[0]!.setData).toHaveBeenCalledWith(dataB);
    });

    it('destroys the instance on unmount', () => {
        const { unmount } = renderChart(dataA);

        unmount();

        expect(instances[0]!.destroy).toHaveBeenCalled();
    });

    it('passes bands into the uPlot options (aggregate view, 7.6)', () => {
        render(
            <ThemeProvider>
                <AxisChart
                    title="Accel"
                    unit="m/s²"
                    series={SERIES}
                    data={dataA}
                    bands={[{ from: 2, to: 1, fill: 'rgba(0,0,0,0.1)' }]}
                />
            </ThemeProvider>,
        );

        expect(instances[0]!.options.bands).toEqual([{ series: [2, 1], fill: 'rgba(0,0,0,0.1)' }]);
    });

    it('applies visibility via setSeries without recreating the instance (7.6)', () => {
        const { rerender } = render(
            <ThemeProvider>
                <AxisChart
                    title="Accel"
                    unit="m/s²"
                    series={SERIES}
                    data={dataA}
                    visibility={[true, true, true]}
                />
            </ThemeProvider>,
        );
        instances[0]!.setSeries.mockClear();

        rerender(
            <ThemeProvider>
                <AxisChart
                    title="Accel"
                    unit="m/s²"
                    series={SERIES}
                    data={dataA}
                    visibility={[true, false, true]}
                />
            </ThemeProvider>,
        );

        expect(instances).toHaveLength(1);
        expect(instances[0]!.setSeries).toHaveBeenCalledWith(2, { show: false });
        expect(instances[0]!.setSeries).toHaveBeenCalledWith(3, { show: true });
    });

    it('reapplies the visibility filter when the canvas is recreated (series change)', () => {
        const OTHER_SERIES: AxisChartSeries[] = [
            { label: 'gx', color: '#f00' },
            { label: 'gy', color: '#0f0' },
            { label: 'gz', color: '#00f' },
        ];
        const { rerender } = render(
            <ThemeProvider>
                <AxisChart
                    title="Accel"
                    unit="m/s²"
                    series={SERIES}
                    data={dataA}
                    visibility={[true, false, true]}
                />
            </ThemeProvider>,
        );

        // A new series reference forces a destroy + recreate — the fresh instance must
        // receive the still-active filter.
        rerender(
            <ThemeProvider>
                <AxisChart
                    title="Accel"
                    unit="m/s²"
                    series={OTHER_SERIES}
                    data={dataA}
                    visibility={[true, false, true]}
                />
            </ThemeProvider>,
        );

        expect(instances).toHaveLength(2);
        expect(instances[1]!.setSeries).toHaveBeenCalledWith(2, { show: false });
    });
});
