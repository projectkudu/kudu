import * as React from 'react';
import { WorkerInfo } from './DataService';
import { Menu } from 'react-data-grid-addons';

type ContextEventHandler = (e: Event, d: { rowIdx: number }) => any;

interface ContextProps {
    onWorkerAdd: ContextEventHandler;
    onWorkerPing: ContextEventHandler;
    onWorkerRenew: ContextEventHandler;
    isManager: (rowIdx: number | undefined) => boolean;
    rowIdx?: number;
    idx?: number;
}

export class WorkerContextMenu extends React.Component<ContextProps, { rows: WorkerInfo[] }> {
    onAdd: ContextEventHandler;
    onRenew: ContextEventHandler;
    onPing: ContextEventHandler;

    constructor() {
        super();
        this.onAdd = (e, d) => this.props.onWorkerAdd(e, d);
        this.onRenew = (e, d) => this.props.onWorkerRenew(e, d);
        this.onPing = (e, d) => this.props.onWorkerPing(e, d);
    }

    render() {
        const addAction = this.props.isManager(this.props.rowIdx)
            ? (<Menu.MenuItem data={{ rowIdx: this.props.rowIdx }} onClick={this.onAdd}>
                    Add
                </Menu.MenuItem>)
            : null;

        return (
            <Menu.ContextMenu>
                {addAction}
                <Menu.MenuItem data={{ rowIdx: this.props.rowIdx }} onClick={this.onRenew}>
                    Renew
                </Menu.MenuItem>
                <Menu.MenuItem data={{ rowIdx: this.props.rowIdx }} onClick={this.onPing}>
                    Ping
                </Menu.MenuItem>
            </Menu.ContextMenu>
        );
    }
}