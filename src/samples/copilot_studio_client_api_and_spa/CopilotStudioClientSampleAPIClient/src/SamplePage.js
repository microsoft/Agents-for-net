import React from 'react';
import './SamplePage.css';

const SamplePage = () => {
    return (
        <div className="sample-page">
            <h1>Lorem Ipsum</h1>
            <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</p>
            <ul>
                <li>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</li>
                <li>Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</li>
                <li>Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</li>
                <li>Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.</li>
                <li>Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.</li>
            </ul>
            <h2>Specifications</h2>
            <table>
                <thead>
                    <tr>
                        <th>Feature</th>
                        <th>Details</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td>Lorem</td>
                        <td>Ipsum dolor sit amet</td>
                    </tr>
                    <tr>
                        <td>Consectetur</td>
                        <td>Adipiscing elit</td>
                    </tr>
                    <tr>
                        <td>Sed</td>
                        <td>Do eiusmod tempor</td>
                    </tr>
                    <tr>
                        <td>Incididunt</td>
                        <td>Ut labore et dolore</td>
                    </tr>
                    <tr>
                        <td>Magna</td>
                        <td>Aliqua</td>
                    </tr>
                </tbody>
            </table>
            <h2>Why Choose Lorem Ipsum?</h2>
            <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.</p>
        </div>
    );
};

export default SamplePage;