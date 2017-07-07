import * as React from 'react';
import * as ReactDataGrid from 'react-data-grid';
import { DataService, WorkerInfo, isWorkerInfo } from './DataService';

//import './App.css';
import { Column } from 'react-data-grid';
import { WorkerContextMenu } from './WorkerContextMenu';

interface AppState { workers: WorkerInfo[]; isLoading: boolean }

export class Scale extends React.Component<{}, AppState> {
    grid: ReactDataGrid | null;
    _columns: WorkerInfo[];
    _getRowAt: (index: number) => WorkerInfo;

    constructor() {
        super();
        this.state = { workers: [], isLoading: true };
        this._getRowAt = this.getWorkerAt.bind(this);
    }

    async componentDidMount() {
        const workers = await DataService.getWorkers();
        if (workers !== null) {
            this.setState({ workers: workers, isLoading: false });
        }
    }

    getColumns(): Column[] {
        let columns = DataService.getWorkersColumns().slice();
        return columns;
    }

    getWorkerAt(index: number): WorkerInfo {
        return this.state.workers[index];
    }

    getSize(): number {
        return this.state.workers.length;
    }

    async addWorker(rowIdx: number) {
        const worker = this.getWorkerAt(rowIdx);

        this.setState({ isLoading: true });

        const result = await DataService.addWorker(worker.id);
        if (isWorkerInfo(result)) {
            const workers = this.state.workers.slice();
            workers.push(result);
            this.setState({ workers: workers });
        }
        this.setState({ isLoading: false });
    }

    async removeWorker(rowIdx: number) {
        const worker = this.getWorkerAt(rowIdx);

        this.setState({ isLoading: true });

        const result = await DataService.removeWorker(worker.id);
        if (isWorkerInfo(result)) {
            const workers = this.state.workers.slice();
            for (let i = 0; i < workers.length; i++) {
                if (workers[i].id === result.id) {
                    workers[i] = result;
                }
            }
            this.setState({ workers: workers });
        }
        this.setState({ isLoading: false });
    }

    async pingWorker(rowIdx: number) {
        const worker = this.getWorkerAt(rowIdx);

        this.setState({ isLoading: true });

        const result = await DataService.pingWorker(worker.id);
        if (isWorkerInfo(result)) {
            const workers = this.state.workers.slice();
            for (let i = 0; i < workers.length; i++) {
                if (workers[i].id === result.id) {
                    workers[i] = result;
                }
            }
            this.setState({ workers: workers });
        }
        this.setState({ isLoading: false });
    }

    isManager(rowIdx: number | undefined): boolean {
        if (typeof rowIdx === 'undefined' || rowIdx < 0) {
            return false;
        } else {
            return this.getWorkerAt(rowIdx).isManager == "true";
        }
    }

    render() {
        const spinner = this.state.isLoading ? <img className="paKman" src="/Content/Images/paKman.gif" title="Please wait" /> : <div />
        return (
            <div>
                <div className="grid-header">
                    <h2>Workers Scale View</h2>
                    <div id="spinner">
                        {spinner}
                    </div>
                </div>
                <div className="grid-container">
                    <ReactDataGrid
                        contextMenu={<WorkerContextMenu
                            onWorkerAdd={(e, d) => this.addWorker(d.rowIdx)}
                            onWorkerRemove={(e, d) => this.removeWorker(d.rowIdx)}
                            onWorkerPing={(e, d) => this.pingWorker(d.rowIdx)}
                            isManager={d => this.isManager(d)}
                        />}
                        ref={node => this.grid = node}
                        columns={this.getColumns()}
                        rowGetter={row => this.getWorkerAt(row)}
                        rowsCount={this.getSize()}
                        rowHeight={50}
                    />
                </div>
            </div>
        );
    }
}
